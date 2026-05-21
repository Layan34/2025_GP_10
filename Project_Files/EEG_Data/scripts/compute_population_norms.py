from __future__ import annotations

"""
compute_population_norms.py
===========================
Run this script ONCE after training to compute population-level TBR and BAR
means from the training dataset. The output is saved to:
    data/models/population_norms.json

These norms are used by 5_runtime_calibration.py as a fallback when a player
is missing one focus state (all focused or all unfocused) during the tutorial.

Important directional notes:
- TBR (Theta/Beta Ratio): HIGHER during unfocused, LOWER during focused.
- BAR (Beta/Alpha Ratio): HIGHER during focused,   LOWER during unfocused.
"""

import json
from pathlib import Path

import numpy as np
import pandas as pd


# ---------------------------------------------------------------------------
# Channels — must match 3_runtime_feature_extraction.py exactly
# ---------------------------------------------------------------------------

CHANNELS = (
    "AF3", "F7", "F3", "FC5", "T7", "P7", "O1", "O2",
    "P8",  "T8", "FC6", "F4", "F8", "AF4",
)

# Column names for TBR and BAR per channel — used to compute cross-channel means
TBR_COLS = [f"tbr_{ch}" for ch in CHANNELS]
BAR_COLS = [f"bar_{ch}" for ch in CHANNELS]


# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------

def base_dir() -> Path:
    return Path(__file__).resolve().parent.parent


def dataset_path() -> Path:
    return base_dir() / "data" / "datasets" / "dataset_final.csv"


# Output path for the JSON file consumed by the runtime calibration module
def norms_output_path() -> Path:
    return base_dir() / "data" / "models" / "population_norms.json"


def ensure_parent(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


# ---------------------------------------------------------------------------
# Main computation
# ---------------------------------------------------------------------------

def compute_norms() -> None:
    path = dataset_path()
    if not path.exists():
        raise FileNotFoundError(f"Training dataset not found: {path}")

    df = pd.read_csv(path, low_memory=False)

    # Validate required columns
    required = {"focus_label_binary"} | set(TBR_COLS) | set(BAR_COLS)
    missing = required - set(df.columns)
    if missing:
        raise ValueError(f"Dataset missing required columns: {sorted(missing)[:10]}")

    # Clean labels
    df["focus_label_binary"] = pd.to_numeric(df["focus_label_binary"], errors="coerce")
    df = df.dropna(subset=["focus_label_binary"]).copy()
    df["focus_label_binary"] = df["focus_label_binary"].astype(int)

    # Compute mean TBR and BAR across all 14 channels per window
    # This must match the aggregation used in calibration/runtime.
    df["tbr_mean"] = df[TBR_COLS].mean(axis=1)
    df["bar_mean"] = df[BAR_COLS].mean(axis=1)

    focused = df[df["focus_label_binary"] == 1].copy()
    unfocused = df[df["focus_label_binary"] == 0].copy()

    if focused.empty:
        raise ValueError("No focused windows found in training dataset.")
    if unfocused.empty:
        raise ValueError("No unfocused windows found in training dataset.")

    # Returns descriptive stats for a series; median and quartiles kept for reference only
    def robust_stats(values: pd.Series) -> dict:
        values = pd.to_numeric(values, errors="coerce").dropna().to_numpy(dtype=float)
        return {
            "mean": float(np.mean(values)),
            "std": float(np.std(values, ddof=0)),
            "median": float(np.median(values)),  # kept for reference only
            "q25": float(np.quantile(values, 0.25)),
            "q75": float(np.quantile(values, 0.75)),
        }

    tbr_focused = robust_stats(focused["tbr_mean"])
    tbr_unfocused = robust_stats(unfocused["tbr_mean"])

    bar_focused = robust_stats(focused["bar_mean"])
    bar_unfocused = robust_stats(unfocused["bar_mean"])

    # Population-wide scale used only when the mean direction is invalid
    # or the focused/unfocused gap is too small for a stable threshold.
    tbr_all = pd.to_numeric(df["tbr_mean"], errors="coerce").dropna().to_numpy(dtype=float)
    bar_all = pd.to_numeric(df["bar_mean"], errors="coerce").dropna().to_numpy(dtype=float)

    tbr_center = float(np.mean(tbr_all))
    bar_center = float(np.mean(bar_all))

    tbr_scale = float(np.std(tbr_all, ddof=0))
    bar_scale = float(np.std(bar_all, ddof=0))

    # Minimum gap is 10% of population SD to ensure the thresholds are meaningfully separated
    min_gap_ratio = 0.10

    def enforce_direction(
        focused_mean: float,
        unfocused_mean: float,
        center: float,
        scale: float,
        direction: str,
    ) -> tuple[float, float, bool, str]:
        min_gap = max(scale * min_gap_ratio, 1e-6)

        if direction == "higher_when_unfocused":
            valid = unfocused_mean > focused_mean
            gap = abs(unfocused_mean - focused_mean)

            # Use raw means if they agree with the expected EEG direction and gap is large enough
            if valid and gap >= min_gap:
                return focused_mean, unfocused_mean, False, "original_means"

            # Fallback: place thresholds symmetrically around population center
            return center - (min_gap / 2), center + (min_gap / 2), True, "direction_corrected_center_gap"

        if direction == "higher_when_focused":
            valid = focused_mean > unfocused_mean
            gap = abs(focused_mean - unfocused_mean)

            if valid and gap >= min_gap:
                return focused_mean, unfocused_mean, False, "original_means"

            return center + (min_gap / 2), center - (min_gap / 2), True, "direction_corrected_center_gap"

        raise ValueError(f"Unknown direction: {direction}")

    # TBR: expected to be higher when unfocused; correct if the data contradicts this
    tbr_focused_threshold, tbr_unfocused_threshold, tbr_corrected, tbr_method = enforce_direction(
        focused_mean=tbr_focused["mean"],
        unfocused_mean=tbr_unfocused["mean"],
        center=tbr_center,
        scale=tbr_scale,
        direction="higher_when_unfocused",
    )

    # BAR: expected to be higher when focused; correct if the data contradicts this
    bar_focused_threshold, bar_unfocused_threshold, bar_corrected, bar_method = enforce_direction(
        focused_mean=bar_focused["mean"],
        unfocused_mean=bar_unfocused["mean"],
        center=bar_center,
        scale=bar_scale,
        direction="higher_when_focused",
    )

    norms = {
            "n_participants": int(df["participant"].nunique()) if "participant" in df.columns else None,
            "n_windows_total": int(len(df)),
            "n_windows_focused": int(len(focused)),
            "n_windows_unfocused": int(len(unfocused)),
            "tbr": {
                "focused_threshold": tbr_focused_threshold,
                "unfocused_threshold": tbr_unfocused_threshold,
                "direction": "higher_when_unfocused",
                "center": tbr_center,
                "scale": tbr_scale,
                "corrected": tbr_corrected,    # True if fallback thresholds were applied
                "method": tbr_method,
                "raw_focused_mean": tbr_focused["mean"],
                "raw_unfocused_mean": tbr_unfocused["mean"],
                "focused_stats": tbr_focused,
                "unfocused_stats": tbr_unfocused,
            },
            "bar": {
                "focused_threshold": bar_focused_threshold,
                "unfocused_threshold": bar_unfocused_threshold,
                "direction": "higher_when_focused",
                "center": bar_center,
                "scale": bar_scale,
                "corrected": bar_corrected,    # True if fallback thresholds were applied
                "method": bar_method,
                "raw_focused_mean": bar_focused["mean"],
                "raw_unfocused_mean": bar_unfocused["mean"],
                "focused_stats": bar_focused,
                "unfocused_stats": bar_unfocused,
            },
            "notes": (
                "Population-level norms computed from the full training dataset using mean and SD. "
                "Mean and SD are used here because the population aggregates many windows across "
                "multiple participants, providing a sufficiently large and stable sample. "
                "If raw means contradict the expected EEG-ratio direction or are too close, "
                "direction-corrected fallback thresholds are generated around the population center. "
                "Used as fallback in calibration when a player is missing one focus state. "
                "TBR is higher during unfocused states. "
                "BAR is higher during focused states."
            ),
        }

    ensure_parent(norms_output_path())
    norms_output_path().write_text(json.dumps(norms, indent=2), encoding="utf-8")

    print(f"[OK] Population norms saved to: {norms_output_path()}")
    print(f"     TBR — focused threshold: {tbr_focused_threshold:.4f} | unfocused threshold: {tbr_unfocused_threshold:.4f}")
    print(f"     BAR — focused threshold: {bar_focused_threshold:.4f} | unfocused threshold: {bar_unfocused_threshold:.4f}")

if __name__ == "__main__":
    compute_norms()