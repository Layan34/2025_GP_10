from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Tuple

import mne
import numpy as np
import pandas as pd


# Config defines sampling rate, frequency bands of interest, channel list, and PSD frequency range
@dataclass(frozen=True)
class Config:
    sfreq: float = 128.0   # Must match the resampled frequency from Step 1
   
    # EEG frequency bands used for power extraction
    bands: Dict[str, Tuple[float, float]] = field(default_factory=lambda: {
        "theta": (4.0, 7.0),   
        "alpha": (8.0, 12.0),
        "beta":  (13.0, 30.0),
    })
    channels: Tuple[str, ...] = (
        "AF3", "F7", "F3", "FC5", "T7", "P7", "O1", "O2",
        "P8", "T8", "FC6", "F4", "F8", "AF4",
    )

    fmin: float = 4.0   # covers theta start
    fmax: float = 30.0  # covers beta end


def base_dir() -> Path:
    return Path(__file__).resolve().parent.parent

def processed_root() -> Path:
    d = base_dir() / "data" / "processed"
    d.mkdir(parents=True, exist_ok=True)
    return d

# Output directory for per-participant feature files and the combined master CSV
def datasets_root() -> Path:
    d = base_dir() / "data" / "datasets"
    d.mkdir(parents=True, exist_ok=True)
    return d


# Validates and cleans the labeled EEG DataFrame: enforces required columns, numeric types, and SAM range 1–5
def prepare_epoch_dataframe(df: pd.DataFrame, cfg: Config) -> pd.DataFrame:
    required = {"epoch", "time_s", "sam_label"}
    missing = required - set(df.columns)
    if missing:
        raise ValueError(f"Missing required columns: {sorted(missing)}")

    missing_ch = [ch for ch in cfg.channels if ch not in df.columns]
    if missing_ch:
        raise ValueError(f"Missing EEG channels: {missing_ch}")

    out = df.copy()
    out["time_s"]    = pd.to_numeric(out["time_s"],    errors="coerce")
    out["sam_label"] = pd.to_numeric(out["sam_label"], errors="coerce")
    out["epoch"]     = pd.to_numeric(out["epoch"],     errors="coerce")

    out = out.dropna(subset=["epoch", "time_s", "sam_label"]).copy()
    out["epoch"]     = out["epoch"].astype(int)
    out["sam_label"] = out["sam_label"].astype(int)
    out = out[out["sam_label"].between(1, 5)].copy()  # Discard any SAM labels outside valid range

    # sort by epoch then relative time within epoch
    sort_col = "time" if "time" in out.columns else "time_s"
    if sort_col == "time":
        out["time"] = pd.to_numeric(out["time"], errors="coerce")
        out = out.dropna(subset=["time"])
    out = out.sort_values(["epoch", sort_col]).reset_index(drop=True)

    if out.empty:
        raise ValueError("No valid epoch rows left after cleaning.")

    return out


# Converts the sample-level DataFrame into a 3D NumPy tensor (n_epochs, n_channels, n_times)
# Also returns onset/offset times, SAM labels, and optional metadata per epoch
def build_epoch_tensor(df: pd.DataFrame, cfg: Config):
    df = prepare_epoch_dataframe(df, cfg)

    X_list, start_list, end_list, y_list, meta_list = [], [], [], [], []

    for epoch_id, g in df.groupby("epoch", sort=True):
        X_list.append(g[list(cfg.channels)].to_numpy(dtype=float).T)  # Shape: (n_channels, n_times)

        # use event onset time if available (absolute wall-clock start of trial)
        if "event_onset_time_s" in g.columns:
            start = float(g["event_onset_time_s"].iloc[0])
        else:
            start = float(g["time_s"].iloc[0])

        start_list.append(start)
        end_list.append(float(g["time_s"].iloc[-1]))
        y_list.append(int(g["sam_label"].iloc[0]))

        # Collect optional metadata columns if present
        meta = {}
        if "block" in g.columns:
            block_val = pd.to_numeric(g["block"].iloc[0], errors="coerce")
            if np.isfinite(block_val):
                meta["block"] = int(block_val)
        if "sam_rating" in g.columns:
            meta["sam_rating"] = g["sam_rating"].iloc[0]
        if "stimulus" in g.columns:
            meta["stimulus"] = str(g["stimulus"].iloc[0])
        meta_list.append(meta)

    if not X_list:
        raise RuntimeError("No complete epochs found.")

    # All epochs must have the same number of time samples
    lengths = [x.shape[1] for x in X_list]
    if len(set(lengths)) != 1:
        raise RuntimeError(f"Inconsistent epoch lengths: {sorted(set(lengths))}")

    X = np.stack(X_list, axis=0)  # Final shape: (n_epochs, n_channels, n_times)
    return X, np.asarray(start_list), np.asarray(end_list), np.asarray(y_list, dtype=int), meta_list


# Computes Power Spectral Density using multitaper method across all epochs and channels
def compute_psd(X: np.ndarray, cfg: Config):
    psds, freqs = mne.time_frequency.psd_array_multitaper(
        X, sfreq=cfg.sfreq, fmin=cfg.fmin, fmax=cfg.fmax, verbose=False,
    )
    return psds, freqs  # psds shape: (n_epochs, n_channels, n_freqs)


# Averages PSD values within a given frequency band -> returns (n_epochs, n_channels)
def band_power(psds: np.ndarray, freqs: np.ndarray, band: tuple[float, float]) -> np.ndarray:
    fmin, fmax = band
    idx = (freqs >= fmin) & (freqs <= fmax)
    if not np.any(idx):
        raise ValueError(f"No frequency bins found for band {band}")
    return psds[:, :, idx].mean(axis=-1)  # -> (n_epochs, n_channels)


# Runs the full feature extraction pipeline for one participant and returns a flat feature DataFrame
def extract_features_for_participant(pid: str, csv_path: Path, cfg: Config) -> pd.DataFrame:
    df = pd.read_csv(csv_path, low_memory=False)

    X, start_time_s, end_time_s, y, meta = build_epoch_tensor(df, cfg)
    psds, freqs = compute_psd(X, cfg)

    # Extract per-channel power for each frequency band
    theta = band_power(psds, freqs, cfg.bands["theta"])
    alpha = band_power(psds, freqs, cfg.bands["alpha"])
    beta  = band_power(psds, freqs, cfg.bands["beta"])

    # Compute derived ratios: TBR (Theta/Beta) and BAR (Beta/Alpha)
    with np.errstate(invalid="ignore", divide="ignore"):
        tbr = np.where(beta != 0, theta / beta, np.nan)   # Theta-to-Beta ratio
        bar = np.where(alpha != 0, beta / alpha, np.nan)  # Beta-to-Alpha ratio

    rows = []
    for i in range(X.shape[0]):
        row = {
            "participant":  pid,
            "start_time_s": float(start_time_s[i]),
            "end_time_s":   float(end_time_s[i]),
            "sam_label":    int(y[i]),
        }
        row.update(meta[i])

        # Store one feature value per channel per band (5 features × 14 channels = 70 columns)
        for ci, ch in enumerate(cfg.channels):
            row[f"theta_{ch}"]         = float(theta[i, ci])
            row[f"alpha_{ch}"]         = float(alpha[i, ci])
            row[f"beta_{ch}"]          = float(beta[i, ci])
            row[f"tbr_{ch}"]           = float(tbr[i, ci])
            row[f"bar_{ch}"]           = float(bar[i, ci])

        rows.append(row)

    return pd.DataFrame(rows)


# Entry point: extracts features for all participants and saves individual + combined master CSVs
def extract_all() -> None:
    print("=== Step 3: Feature Extraction ===")
    cfg = Config()

    labeled_files = sorted((processed_root()).glob("P*_labeled.csv"))
    if not labeled_files:
        raise FileNotFoundError(f"No P*_labeled.csv found in: {processed_root()}")

    all_dfs = []
    for path in labeled_files:
        pid = path.name.split("_")[0]
        try:
            df_feat  = extract_features_for_participant(pid, path, cfg)
            out_path = datasets_root() / f"{pid}_features.csv"
            df_feat.to_csv(out_path, index=False)
            all_dfs.append(df_feat)
            print(f"[OK] {pid}: {out_path.name} (epochs={len(df_feat):,})")
        except Exception as exc:
            print(f"[Fail] {pid}: {exc}")

    # Concatenate all participants into a single master feature file
    if all_dfs:
        master      = pd.concat(all_dfs, ignore_index=True)
        master_path = datasets_root() / "master_features.csv"
        master.to_csv(master_path, index=False)
        print(f"[OK] Master: {master_path.name} (rows={len(master):,})")

    print("=== Done ===")


if __name__ == "__main__":
    extract_all()