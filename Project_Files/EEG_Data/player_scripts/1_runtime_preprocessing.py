from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Tuple
import json

import mne
import numpy as np
import pandas as pd

# Preprocessing settings: sampling rates, filter cutoffs, epoch window, and channel list
@dataclass(frozen=True)
class Config:
    raw_sfreq: float       = 163.2  # Original device sampling frequency (Hz)
    sfreq:     float       = 128.0  # Target resampled frequency (Hz)
    l_freq:    float       = 0.5    # High-pass cutoff — removes slow drifts
    h_freq:    float       = 30.0   # Low-pass cutoff — removes high-freq noise
    notch_freq: float      = 50.0   # Notch filter — removes power-line interference

    tmin: float = 0.0  # Epoch start relative to stimulus onset (seconds)
    tmax: float = 1    # Epoch end relative to stimulus onset (seconds)

    # 14-channel Emotiv EPOC layout
    channels: Tuple[str, ...] = (
        "AF3", "F7", "F3", "FC5", "T7", "P7", "O1", "O2",
        "P8", "T8", "FC6", "F4", "F8", "AF4",
    )


def base_dir() -> Path:
    return Path(__file__).resolve().parent.parent

# Returns the directory holding the current player's raw session files
def current_player_dir() -> Path:
    return base_dir() / "player_data" / "current_player"

# Returns the output directory for processed player data; creates it if missing
def processed_root() -> Path:
    out = base_dir() / "player_data" / "processed"
    out.mkdir(parents=True, exist_ok=True)
    return out


# Reads EEG CSV, keeps required columns, coerces to numeric, and drops NaN rows
def read_eeg_csv(path: Path, cfg: Config) -> pd.DataFrame:
    df = pd.read_csv(path, low_memory=False)

    required = ["TimestampLSL_s"] + [f"EEG.{ch}" for ch in cfg.channels]
    missing  = [c for c in required if c not in df.columns]
    if missing:
        raise ValueError(f"Missing columns: {missing}")

    eeg_df = df[required].copy()
    eeg_df = eeg_df.apply(pd.to_numeric, errors="coerce").dropna().reset_index(drop=True)
    eeg_df["time_s"] = eeg_df["TimestampLSL_s"]

    if eeg_df.empty:
        raise ValueError("EEG data is empty after cleaning.")
    return eeg_df


# Converts EEG DataFrame into an MNE RawArray with standard 10-20 montage
def build_raw(eeg_df: pd.DataFrame, cfg: Config) -> mne.io.RawArray:
    data = eeg_df[[f"EEG.{ch}" for ch in cfg.channels]].to_numpy(dtype=float).T.copy()
    data *= 1e-6  # µV → V (MNE expects volts)

    info = mne.create_info(
        ch_names=list(cfg.channels), sfreq=cfg.raw_sfreq, ch_types="eeg"
    )
    raw = mne.io.RawArray(data, info, verbose=False)
    raw.set_montage(mne.channels.make_standard_montage("standard_1020"))
    return raw


# Parses Markers.csv and builds a trial onset table with times relative to EEG start
def build_trial_table(markers_path: Path, eeg_start_time: float) -> pd.DataFrame:
    markers_df = pd.read_csv(markers_path, low_memory=False)

    required = ["TimestampLSL_s", "Marker"]
    missing  = [c for c in required if c not in markers_df.columns]
    if missing:
        raise ValueError(f"Missing columns: {missing}")

    rows: list[dict] = []
    for _, row in markers_df.iterrows():
        try:
            marker_json = json.loads(str(row["Marker"]).strip())
        except json.JSONDecodeError:
            continue  # Skip rows with invalid JSON markers

        if marker_json.get("event") != "trial_onset":
            continue  # Only process trial onset events

        stimulus = str(marker_json.get("stimulus", "")).strip()
        if stimulus not in ("target", "nonTarget"):
            continue  # Only keep target and non-target stimuli

        marker_time = pd.to_numeric(row["TimestampLSL_s"], errors="coerce")
        if pd.isna(marker_time):
            continue

        # SOURCE: https://mne.tools/stable/generated/mne.Annotations.html
        rows.append({
            "marker_time_s":      float(marker_time),
            "annotation_onset_s": float(marker_time) - eeg_start_time,  # Time relative to EEG start
            "stimulus":           stimulus,
            "marker_block":       pd.to_numeric(marker_json.get("block"),  errors="coerce"),
            "marker_trial_index": pd.to_numeric(marker_json.get("trial"), errors="coerce"),
        })

    if not rows:
        raise RuntimeError("No valid trial_onset markers found.")

    return pd.DataFrame(rows).sort_values("marker_time_s").reset_index(drop=True)


# Converts trial table into MNE Annotations (instantaneous events, duration=0)
def build_annotations(trial_table: pd.DataFrame) -> mne.Annotations:
    # SOURCE: https://mne.tools/stable/generated/mne.Annotations.html
    return mne.Annotations(
        onset       = trial_table["annotation_onset_s"].to_numpy(dtype=float),
        duration    = np.zeros(len(trial_table), dtype=float),  # Instantaneous events
        description = trial_table["stimulus"].astype(str).tolist(),
    )


# Applies band-pass, notch, average reference, and resampling to the raw signal
def apply_preprocessing(raw: mne.io.RawArray, cfg: Config) -> mne.io.Raw:
    cleaned = raw.copy()

    # 1. Band-pass: remove slow drifts (< 0.5 Hz) and high-frequency noise (> 30 Hz)
    cleaned.filter(l_freq=cfg.l_freq, h_freq=cfg.h_freq, verbose=False)

    # 2. Notch filter: remove 50 Hz power-line interference
    cleaned.notch_filter(freqs=cfg.notch_freq, verbose=False)

    # 3. Average reference
    cleaned.set_eeg_reference("average", verbose=False)

    # 4. Resample to target sampling frequency
    cleaned.resample(sfreq=cfg.sfreq, verbose=False)

    return cleaned


# Converts MNE Epochs to a flat DataFrame, matching each epoch to its original marker timestamp
def epochs_to_dataframe(
    epochs: mne.Epochs,
    trial_table: pd.DataFrame,
) -> pd.DataFrame:

    epochs_df = epochs.to_data_frame(index=None)

    kept_event_samples = epochs.events[:, 0].astype(int)
    sfreq = epochs.info["sfreq"]

    # Convert trial onset times to sample indices for matching
    trial_copy = trial_table.copy()
    trial_copy["_sample"] = (
        trial_copy["annotation_onset_s"] * sfreq
    ).round().astype(int)

    # Match each epoch's event sample to the nearest trial table entry
    marker_times = []
    for s in kept_event_samples:
        diffs = np.abs(trial_copy["_sample"].to_numpy() - s)
        nearest_idx = int(np.argmin(diffs))
        if diffs[nearest_idx] > 5:  # Tolerance: max 5 samples mismatch
            raise RuntimeError(
                f"Event at sample {s} could not be matched to any trial "
                f"(nearest diff = {diffs[nearest_idx]} samples)."
            )
        marker_times.append(float(trial_copy["marker_time_s"].iloc[nearest_idx]))

    kept_onset_times = np.array(marker_times, dtype=float)

    unique_epochs = np.unique(epochs_df["epoch"].to_numpy())
    if len(kept_onset_times) != len(unique_epochs):
        raise RuntimeError("Epoch count mismatch between events and trial table.")

    # Build lookup maps from epoch ID to onset time and sample index
    onset_map  = {int(eid): kept_onset_times[i]       for i, eid in enumerate(unique_epochs)}
    sample_map = {int(eid): int(kept_event_samples[i]) for i, eid in enumerate(unique_epochs)}

    # Add absolute time column (epoch onset + relative time within epoch)
    epochs_df.insert(0, "time_s",
        epochs_df.apply(
            lambda r: onset_map[int(r["epoch"])] + float(r["time"]), axis=1
        )
    )
    epochs_df["event_onset_time_s"] = epochs_df["epoch"].map(onset_map)
    epochs_df["event_sample"]       = epochs_df["epoch"].map(sample_map)
    epochs_df["stimulus"]           = epochs_df["condition"].astype(str)
    epochs_df = epochs_df.drop(columns=["condition"])

    return epochs_df


# Full runtime pipeline for the current player: load → preprocess → epoch → save CSV
def preprocess_current_player(cfg: Config) -> None:
    player_dir   = current_player_dir()
    eeg_path     = player_dir / "EEG.csv"
    markers_path = player_dir / "Markers.csv"

    if not eeg_path.exists():
        raise FileNotFoundError(f"Missing EEG.csv in {player_dir}")
    if not markers_path.exists():
        raise FileNotFoundError(f"Missing Markers.csv in {player_dir}")

    eeg_df         = read_eeg_csv(eeg_path, cfg)
    eeg_start_time = float(eeg_df["time_s"].iloc[0])  # Used to align marker times to EEG

    raw       = build_raw(eeg_df, cfg)
    raw_clean = apply_preprocessing(raw, cfg)

    trial_table = build_trial_table(markers_path, eeg_start_time)
    raw_clean.set_annotations(build_annotations(trial_table))

    # SOURCE: https://mne.tools/stable/generated/mne.events_from_annotations.html
    events, event_id = mne.events_from_annotations(raw_clean, verbose=False)

    # Sanity check: number of detected events must match number of markers
    if len(events) != len(trial_table):
        raise RuntimeError(
            f"Event count mismatch "
            f"({len(events)} events vs {len(trial_table)} markers)."
        )

    # SOURCE: https://mne.tools/stable/generated/mne.Epochs.html
    epochs = mne.Epochs(
        raw_clean,
        events   = events,
        event_id = event_id,
        tmin     = cfg.tmin,
        tmax     = cfg.tmax,
        baseline = None,  # No baseline correction applied
        preload  = True,
        verbose  = False,
    )

    epochs_df   = epochs_to_dataframe(epochs=epochs, trial_table=trial_table)
    output_path = processed_root() / "current_player_clean.csv"
    epochs_df.to_csv(output_path, index=False)

    print(f"[OK] current_player: {len(epochs)} epochs | event_id={event_id} -> {output_path.name}")


def main() -> None:
    print("=== Step 1: Runtime Preprocessing (current player) ===")
    cfg = Config()

    try:
        preprocess_current_player(cfg)
    except Exception as exc:
        print(f"[Fail] current_player: {exc}")
        raise

    print("=== Done ===")


if __name__ == "__main__":
    main()