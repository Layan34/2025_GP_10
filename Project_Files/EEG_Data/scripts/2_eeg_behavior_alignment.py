from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict

import numpy as np
import pandas as pd


# Config holds the SAM rating mapping (text → integer) and the max allowed time gap for epoch matching
@dataclass(frozen=True)
class Config:
    sam_text_map: Dict[str, int] = field(default_factory=lambda: {
        "Low":         1,
        "Medium-Low":  2,
        "Medium":      3,
        "Medium-High": 4,
        "High":        5,
    })
    onset_tolerance_s: float = 0.05  # Max time difference (seconds) allowed when matching epochs to trials


def base_dir() -> Path:
    return Path(__file__).resolve().parent.parent

def raw_root() -> Path:
    return base_dir() / "data" / "raw"

def processed_root() -> Path:
    out = base_dir() / "data" / "processed"
    out.mkdir(parents=True, exist_ok=True)
    return out


# Converts a SAM rating (text or digit) to an integer 1–5; returns NaN if invalid
def parse_sam_to_int(value: object, cfg: Config) -> int | float:
    if pd.isna(value):
        return np.nan
    text = str(value).strip()
    if text.isdigit():
        n = int(text)
        return n if 1 <= n <= 5 else np.nan  # Reject values outside valid SAM range
    return cfg.sam_text_map.get(text, np.nan)


# Builds a clean trial-level table from Behavior.csv, with SAM labels attached per block
def build_trial_label_table(behavior: pd.DataFrame, cfg: Config) -> pd.DataFrame:
    required = ["lsl_time_s", "row_type", "block", "sam_rating",
                "stimulus", "outcome", "rt_ms", "excluded_fast"]
    missing = [c for c in required if c not in behavior.columns]
    if missing:
        raise ValueError(f"Behavior.csv missing columns: {missing}")

    behavior_df = behavior.copy()
    behavior_df["lsl_time_s"] = pd.to_numeric(behavior_df["lsl_time_s"], errors="coerce")

    # Keep only rows marked as actual trials (not SAM ratings or other row types)
    trial_rows = behavior_df[
        behavior_df["row_type"].astype(str).str.lower().eq("trial")
    ].copy()
    trial_rows = trial_rows.dropna(subset=["lsl_time_s", "block"]).reset_index(drop=True)

    if trial_rows.empty:
        raise ValueError("No valid trial rows found in Behavior.csv.")

    # Remove trials flagged as too fast (likely button-press errors)
    trial_rows["excluded_fast"] = pd.to_numeric(
        trial_rows["excluded_fast"], errors="coerce"
    ).fillna(0)
    n_excluded = int((trial_rows["excluded_fast"] == 1).sum())
    if n_excluded > 0:
        print(f"  [Info] Removing {n_excluded} trial(s) with excluded_fast=1.")
    trial_rows = trial_rows[trial_rows["excluded_fast"] != 1].copy()

    # Extract SAM rows and parse their ratings to integers
    sam_rows = behavior_df[
        behavior_df["row_type"].astype(str).str.lower().eq("sam")
    ].copy()
    sam_rows["sam_label"] = sam_rows["sam_rating"].map(
        lambda v: parse_sam_to_int(v, cfg)
    )

    # One SAM label per block (use the last entry if duplicates exist)
    sam_label_map = (
        sam_rows.dropna(subset=["sam_label"])
        .drop_duplicates(subset=["block"], keep="last")
        .set_index("block")["sam_label"]
        .to_dict()
    )
    sam_rating_map = (
        sam_rows.drop_duplicates(subset=["block"], keep="last")
        .set_index("block")["sam_rating"]
        .to_dict()
    )

    trial_rows["block"]      = pd.to_numeric(trial_rows["block"], errors="coerce").astype("Int64")
    trial_rows["stimulus"]   = trial_rows["stimulus"].astype(str).str.strip()
    trial_rows["sam_label"]  = trial_rows["block"].map(sam_label_map)   # Attach SAM label from block
    trial_rows["sam_rating"] = trial_rows["block"].map(sam_rating_map)  # Attach raw SAM text from block
    trial_rows["outcome"]    = trial_rows["outcome"].astype(str).str.strip()
    trial_rows["rt_ms"]      = pd.to_numeric(trial_rows["rt_ms"], errors="coerce")

    # Drop trials with no SAM label (e.g., missing block mapping)
    trial_rows = trial_rows.dropna(subset=["sam_label"]).copy()
    trial_rows["sam_label"] = trial_rows["sam_label"].astype(int)
    trial_rows["block"]     = trial_rows["block"].astype(int)

    return trial_rows.sort_values("lsl_time_s").reset_index(drop=True)


# Collapses the sample-level EEG DataFrame to one row per epoch with its onset time and stimulus
def build_epoch_table(eeg: pd.DataFrame) -> pd.DataFrame:
    required = ["epoch", "event_onset_time_s", "stimulus"]
    missing = [c for c in required if c not in eeg.columns]
    if missing:
        raise ValueError(f"Clean EEG file missing columns: {missing}")

    epoch_rows = (
        eeg.groupby("epoch", as_index=False)
        .agg(
            event_onset_time_s=("event_onset_time_s", "first"),
            stimulus=("stimulus", "first"),
        )
        .sort_values("event_onset_time_s")
        .reset_index(drop=True)
    )
    epoch_rows["event_onset_time_s"] = pd.to_numeric(
        epoch_rows["event_onset_time_s"], errors="coerce"
    )
    epoch_rows = epoch_rows.dropna(subset=["event_onset_time_s"]).reset_index(drop=True)

    if epoch_rows.empty:
        raise RuntimeError("No valid epochs found in cleaned EEG file.")

    return epoch_rows


# Matches EEG epochs to behavior trials by nearest timestamp (within tolerance), then validates stimulus agreement
def match_epochs_to_trials(
    epoch_table: pd.DataFrame,
    trial_table: pd.DataFrame,
    cfg: Config,
) -> pd.DataFrame:
    epoch_table = epoch_table.copy().sort_values("event_onset_time_s").reset_index(drop=True)
    trial_table = trial_table.copy().sort_values("lsl_time_s").reset_index(drop=True)

    # Nearest-neighbor merge on timestamp — each epoch is matched to the closest trial onset
    merged = pd.merge_asof(
        epoch_table,
        trial_table[[
            "lsl_time_s", "block", "sam_label", "sam_rating",
            "stimulus", "outcome", "rt_ms",
        ]],
        left_on="event_onset_time_s",
        right_on="lsl_time_s",
        direction="nearest",
        tolerance=cfg.onset_tolerance_s,
    )

    # Drop epochs that had no trial match within the allowed time window
    unmatched = merged["lsl_time_s"].isna().sum()
    if unmatched > 0:
        print(
            f"  [Warn] {unmatched} epoch(s) had no behavior match within "
            f"{cfg.onset_tolerance_s}s tolerance — dropped."
        )
    merged = merged.dropna(subset=["lsl_time_s"]).reset_index(drop=True)

    if merged.empty:
        raise RuntimeError("No epochs matched any behavior trial.")

    # Verify that the stimulus label in EEG matches the one in Behavior for every matched pair
    stim_eeg = merged["stimulus_x"].astype(str).str.strip().str.lower()
    stim_beh = merged["stimulus_y"].astype(str).str.strip().str.lower()
    mismatches = int((stim_eeg != stim_beh).sum())
    if mismatches > 0:
        raise RuntimeError(f"Stimulus mismatch on {mismatches} matched epoch(s).")

    merged = merged.rename(columns={"stimulus_x": "stimulus"})
    merged = merged.drop(columns=["stimulus_y", "lsl_time_s"])
    merged["block"]     = merged["block"].astype(int)
    merged["sam_label"] = merged["sam_label"].astype(int)

    return merged[[
        "epoch", "event_onset_time_s", "stimulus",
        "block", "sam_label", "sam_rating",
        "outcome", "rt_ms",
    ]]


# Joins epoch-level labels back onto the full sample-level EEG DataFrame
def attach_epoch_labels_to_samples(
    eeg: pd.DataFrame,
    epoch_labels: pd.DataFrame,
) -> pd.DataFrame:
    merge_cols = [
        "epoch", "block", "sam_label", "sam_rating",
        "outcome", "rt_ms",
    ]
    labeled = eeg.merge(epoch_labels[merge_cols], on="epoch", how="left")
    # Drop samples whose epoch had no valid label (e.g., unmatched epochs)
    labeled = labeled.dropna(subset=["sam_label"]).reset_index(drop=True)

    if labeled.empty:
        raise RuntimeError("No EEG samples kept after attaching labels.")

    labeled["block"]     = labeled["block"].astype(int)
    labeled["sam_label"] = labeled["sam_label"].astype(int)

    return labeled


# Full pipeline for one participant: load → build tables → match → attach labels → save
def process_one_participant(pid_dir: Path, cfg: Config) -> None:
    pid           = pid_dir.name
    eeg_path      = processed_root() / f"{pid}_clean.csv"   # Output from Step 1
    behavior_path = pid_dir / "Behavior.csv"

    if not eeg_path.exists():
        raise FileNotFoundError(f"Missing cleaned EEG: {eeg_path.name}")
    if not behavior_path.exists():
        raise FileNotFoundError("Missing Behavior.csv")

    eeg      = pd.read_csv(eeg_path,      low_memory=False)
    behavior = pd.read_csv(behavior_path, low_memory=False)

    trial_table  = build_trial_label_table(behavior, cfg)
    epoch_table  = build_epoch_table(eeg)
    epoch_labels = match_epochs_to_trials(epoch_table, trial_table, cfg)
    labeled_eeg  = attach_epoch_labels_to_samples(eeg, epoch_labels)

    output_path = processed_root() / f"{pid}_labeled.csv"
    labeled_eeg.to_csv(output_path, index=False)

    print(
        f"[OK] {pid}: {output_path.name} | "
        f"epochs={epoch_labels['epoch'].nunique()} | "
        f"samples={len(labeled_eeg):,}"
    )


# Entry point: runs Step 2 for all participant directories
def main() -> None:
    print("=== Step 2: EEG-Behavior Alignment ===")
    cfg = Config()

    participant_dirs = sorted(
        [p for p in raw_root().glob("P*") if p.is_dir()],
        key=lambda p: int(p.name[1:]),
    )

    for pid_dir in participant_dirs:
        try:
            process_one_participant(pid_dir, cfg)
        except Exception as exc:
            print(f"[Fail] {pid_dir.name}: {exc}")  # Log failure and continue to next participant

    print("=== Done ===")


if __name__ == "__main__":
    main()