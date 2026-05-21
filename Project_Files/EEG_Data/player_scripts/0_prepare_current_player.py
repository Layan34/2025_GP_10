from __future__ import annotations

from pathlib import Path
import shutil
import json

import numpy as np
import pandas as pd
import pyxdf


# The 14 Emotiv EPOC channels expected in the EEG stream
EXPECTED_CHANNELS = [
    "AF3", "F7", "F3", "FC5", "T7", "P7", "O1", "O2",
    "P8", "T8", "FC6", "F4", "F8", "AF4",
]


def base_dir() -> Path:
    return Path(__file__).resolve().parent.parent


# Checks macOS path first, then Windows; defaults to macOS if neither exists
def unity_save_dir() -> Path:
    home = Path.home()

    mac_path = home / "Library" / "Application Support" / "DefaultCompany" / "GP1"
    win_path = home / "AppData" / "LocalLow" / "DefaultCompany" / "GP1"

    if mac_path.exists():
        return mac_path
    if win_path.exists():
        return win_path

    return mac_path


# Returns the working directory for the current player session; creates it if missing
def current_player_dir() -> Path:
    d = base_dir() / "player_data" / "current_player"
    d.mkdir(parents=True, exist_ok=True)
    return d


# Copies src to dst; raises FileNotFoundError if src doesn't exist
def _safe_copy(src: Path, dst: Path) -> None:
    if not src.exists():
        raise FileNotFoundError(f"Missing source file: {src}")
    shutil.copy2(src, dst)


# Extracts the stream name from XDF metadata; returns empty string on failure
def _get_stream_name(stream: dict) -> str:
    try:
        return stream["info"]["name"][0]
    except Exception:
        return ""


# Extracts the stream type (e.g. "EEG", "Markers") from XDF metadata
def _get_stream_type(stream: dict) -> str:
    try:
        return stream["info"]["type"][0]
    except Exception:
        return ""


# Reads channel label strings from the XDF stream description metadata
def _get_channel_labels(stream: dict) -> list[str]:
    try:
        channels = stream["info"]["desc"][0]["channels"][0]["channel"]
        labels = []
        for ch in channels:
            label = ch.get("label", [""])[0]
            labels.append(str(label).strip())
        return labels
    except Exception:
        return []


# Identifies a marker stream by type/name keyword or by single-element row structure
def _is_marker_stream(stream: dict) -> bool:
    stream_type = _get_stream_type(stream).lower()
    stream_name = _get_stream_name(stream).lower()

    if "marker" in stream_type or "marker" in stream_name:
        return True

    ts = stream.get("time_series", [])
    if len(ts) == 0:
        return False

    first = ts[0]
    if isinstance(first, (list, tuple, np.ndarray)) and len(first) == 1:
        return True

    return False


# Identifies an EEG stream by type/name keyword or by matching at least 8 expected channel labels
def _is_eeg_stream(stream: dict) -> bool:
    stream_type = _get_stream_type(stream).lower()
    stream_name = _get_stream_name(stream).lower()
    labels = _get_channel_labels(stream)

    if "eeg" in stream_type or "eeg" in stream_name:
        return True

    overlap = sum(1 for ch in EXPECTED_CHANNELS if ch in labels)
    return overlap >= 8


# Normalizes a marker value (list, array, or scalar) to a plain string
def _extract_marker_text(value) -> str:
    if isinstance(value, (list, tuple, np.ndarray)):
        if len(value) == 0:
            return ""
        return str(value[0]).strip()
    return str(value).strip()


# Returns the first EEG and marker streams found; raises if either is missing
def find_streams(streams: list[dict]) -> tuple[dict, dict]:
    eeg_candidates = [s for s in streams if _is_eeg_stream(s)]
    marker_candidates = [s for s in streams if _is_marker_stream(s)]

    if not eeg_candidates:
        names = [(_get_stream_name(s), _get_stream_type(s)) for s in streams]
        raise RuntimeError(f"Could not find EEG stream in XDF. Streams found: {names}")

    if not marker_candidates:
        names = [(_get_stream_name(s), _get_stream_type(s)) for s in streams]
        raise RuntimeError(f"Could not find Marker stream in XDF. Streams found: {names}")

    eeg_stream = eeg_candidates[0]
    marker_stream = marker_candidates[0]
    return eeg_stream, marker_stream


# Converts the XDF EEG stream to a CSV with TimestampLSL_s and one column per channel
def extract_eeg_csv(eeg_stream: dict, out_path: Path) -> None:
    time_stamps = np.asarray(eeg_stream["time_stamps"], dtype=float)
    time_series = np.asarray(eeg_stream["time_series"], dtype=float)

    if time_series.ndim != 2:
        raise RuntimeError("EEG stream time_series is not 2D.")

    labels = _get_channel_labels(eeg_stream)

    # If labels are missing or incomplete, fall back to first 14 expected channels
    if len(labels) < time_series.shape[1]:
        labels = EXPECTED_CHANNELS[: time_series.shape[1]]

    df = pd.DataFrame()
    df["TimestampLSL_s"] = time_stamps

    for i in range(min(len(labels), time_series.shape[1])):
        label = labels[i].strip()
        if not label:
            label = f"CH{i+1}"
        df[f"EEG.{label}"] = time_series[:, i]

    missing_expected = [f"EEG.{ch}" for ch in EXPECTED_CHANNELS if f"EEG.{ch}" not in df.columns]
    if missing_expected:
        raise RuntimeError(
            "Extracted EEG stream is missing expected channels: "
            + ", ".join(missing_expected)
        )

    # Keep only required columns in the same style your preprocessing expects
    keep_cols = ["TimestampLSL_s"] + [f"EEG.{ch}" for ch in EXPECTED_CHANNELS]
    df = df[keep_cols]

    df.to_csv(out_path, index=False)


# Converts the XDF marker stream to a CSV with TimestampLSL_s and Marker text columns
def extract_markers_csv(marker_stream: dict, out_path: Path) -> None:
    time_stamps = np.asarray(marker_stream["time_stamps"], dtype=float)
    time_series = marker_stream["time_series"]

    markers = [_extract_marker_text(x) for x in time_series]

    df = pd.DataFrame({
        "TimestampLSL_s": time_stamps,
        "Marker": markers,
    })

    df.to_csv(out_path, index=False)


# Full pipeline: copies Unity files, loads XDF, extracts EEG and Markers as CSVs
def copy_and_extract() -> None:
    src_dir = unity_save_dir()
    dst_dir = current_player_dir()

    behavior_src = src_dir / "Behavior.csv"
    xdf_src = src_dir / "recording.xdf"

    behavior_dst = dst_dir / "Behavior.csv"
    xdf_dst = dst_dir / "recording.xdf"
    eeg_dst = dst_dir / "EEG.csv"
    markers_dst = dst_dir / "Markers.csv"

    _safe_copy(behavior_src, behavior_dst)
    _safe_copy(xdf_src, xdf_dst)

    streams, _ = pyxdf.load_xdf(str(xdf_dst))
    eeg_stream, marker_stream = find_streams(streams)

    extract_eeg_csv(eeg_stream, eeg_dst)
    extract_markers_csv(marker_stream, markers_dst)


def main() -> None:
    try:
        copy_and_extract()
    except Exception as exc:
        print(f"[FAIL] Step 0 failed: {exc}")
        raise


if __name__ == "__main__":
    main()