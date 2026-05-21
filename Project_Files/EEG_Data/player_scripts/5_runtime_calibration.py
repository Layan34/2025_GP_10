from __future__ import annotations

"""
5_runtime_calibration.py
=========================
Computes personalized TBR and BAR thresholds from the player's tutorial session.

Directional notes (backed by literature):
- TBR (Theta/Beta Ratio): HIGHER during unfocused, LOWER during focused.
  → focused_threshold   = median of focused windows   (lower value)
  → unfocused_threshold = median of unfocused windows (higher value)
- BAR (Beta/Alpha Ratio): HIGHER during focused, LOWER during unfocused.
  → focused_threshold   = median of focused windows   (higher value)
  → unfocused_threshold = median of unfocused windows (lower value)

Fallback (population norms):
When a player is missing one focus state (all focused or all unfocused),
the corresponding population-level threshold from population_norms.json is used
instead of an arbitrary guess.
"""

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Optional

import numpy as np
import pandas as pd


# ---------------------------------------------------------------------------
# Channels 
# ---------------------------------------------------------------------------

# Must match the channel list used in feature extraction
CHANNELS = (
    "AF3", "F7", "F3", "FC5", "T7", "P7", "O1", "O2",
    "P8",  "T8", "FC6", "F4", "F8", "AF4",
)

# Column names for per-channel TBR and BAR features
TBR_COLS = [f"tbr_{ch}" for ch in CHANNELS]
BAR_COLS = [f"bar_{ch}" for ch in CHANNELS]


# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

# All paths and settings for the calibration step
@dataclass(frozen=True)
class CalibrationConfig:
    tutorial_features_path: Path
    population_norms_path: Path      # Precomputed from the training dataset
    output_path: Path                # JSON file consumed by the inference server
    participant_id: str = "current_player"
    min_scale: float = 1e-6          # Floor for IQR to prevent division by zero


# ---------------------------------------------------------------------------
# Path helpers
# ---------------------------------------------------------------------------

def base_dir() -> Path:
    return Path(__file__).resolve().parent.parent


def ensure_parent(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


# ---------------------------------------------------------------------------
# Population norms loading
# ---------------------------------------------------------------------------

# Loads population_norms.json; raises with a clear message if the file is missing
def load_population_norms(path: Path) -> Dict:
    if not path.exists():
        raise FileNotFoundError(
            f"Population norms file not found: {path}\n"
            "Please run compute_population_norms.py first."
        )
    return json.loads(path.read_text(encoding="utf-8"))


# ---------------------------------------------------------------------------
# Data loading
# ---------------------------------------------------------------------------

# Loads the tutorial feature CSV and removes SAM=3 (ambiguous neutral) rows
def load_tutorial_data(path: Path) -> pd.DataFrame:
    df = pd.read_csv(path, low_memory=False)

    # Validate TBR/BAR channel columns exist
    if "tbr_AF3" not in df.columns or "bar_AF3" not in df.columns:
        raise ValueError("Missing TBR/BAR channel columns in tutorial features.")

    # Keep only clear SAM labels: 1, 2, 4, 5 (exclude neutral SAM=3)
    sam = pd.to_numeric(df["sam_label"], errors="coerce")
    df = df[sam.isin([1, 2, 4, 5])].copy()

    if df.empty:
        raise ValueError("No valid tutorial rows remain after excluding SAM=3.")

    return df


# ---------------------------------------------------------------------------
# Label derivation
# ---------------------------------------------------------------------------

# SAM 4-5 → focused (1), SAM 1-2 → unfocused (0)
def derive_binary_labels(df: pd.DataFrame) -> np.ndarray:
    sam = pd.to_numeric(df["sam_label"], errors="coerce")
    return np.where(sam.between(4, 5), 1, 0).astype(int)


# ---------------------------------------------------------------------------
# Ratio extraction
# ---------------------------------------------------------------------------

# Mean across all 14 channels per window — matches inference server aggregation
def extract_ratios(df: pd.DataFrame) -> tuple[np.ndarray, np.ndarray]:
    tbr = df[TBR_COLS].mean(axis=1).to_numpy(dtype=float)
    bar = df[BAR_COLS].mean(axis=1).to_numpy(dtype=float)

    if np.isnan(tbr).any() or np.isnan(bar).any():
        raise ValueError("TBR or BAR contain NaN values after cleaning.")

    return tbr, bar


# ---------------------------------------------------------------------------
# Threshold computation per ratio
# ---------------------------------------------------------------------------

# Computes player-specific TBR thresholds using median; falls back to scaled population norm if a class is missing
def compute_tbr_thresholds(
    values: np.ndarray,
    labels: np.ndarray,
    norms: Dict,
    min_scale: float,
) -> Dict[str, object]:
    center = float(np.median(values))
    scale  = float(np.quantile(values, 0.75) - np.quantile(values, 0.25))
    scale  = max(scale, min_scale)  # Prevent zero IQR

    focused_vals   = values[labels == 1]
    unfocused_vals = values[labels == 0]

    if focused_vals.size > 0:
        focused_threshold = float(np.median(focused_vals))
        fallback_focused  = False
    else:
        # Scale population norm by player's unfocused level relative to group
        scale_factor      = (float(np.median(unfocused_vals)) / norms["tbr"]["unfocused_threshold"]) if unfocused_vals.size > 0 else 1.0
        focused_threshold = float(norms["tbr"]["focused_threshold"]) * scale_factor
        fallback_focused  = True
        print(f"[Calibration] TBR: no focused windows — using scaled population norm: {focused_threshold:.4f} (scale={scale_factor:.4f})")

    if unfocused_vals.size > 0:
        unfocused_threshold = float(np.median(unfocused_vals))
        fallback_unfocused  = False
    else:
        # Scale population norm by player's focused level relative to group
        scale_factor        = (float(np.median(focused_vals)) / norms["tbr"]["focused_threshold"]) if focused_vals.size > 0 else 1.0
        unfocused_threshold = float(norms["tbr"]["unfocused_threshold"]) * scale_factor
        fallback_unfocused  = True
        print(f"[Calibration] TBR: no unfocused windows — using scaled population norm: {unfocused_threshold:.4f} (scale={scale_factor:.4f})")

    return {
        "focused_threshold":   focused_threshold,
        "unfocused_threshold": unfocused_threshold,
        "direction":           "higher_when_unfocused",
        "fallback_focused":    fallback_focused,    # True if population norm was used
        "fallback_unfocused":  fallback_unfocused,  # True if population norm was used
        "center": center,
        "scale":  scale,
        "summary": {
            "min":  float(np.min(values)),
            "max":  float(np.max(values)),
            "mean": float(np.mean(values)),        # kept for reference only
            "std":  float(np.std(values, ddof=0)), # kept for reference only
            "iqr":  float(np.quantile(values, 0.75) - np.quantile(values, 0.25)),
            "q25":  float(np.quantile(values, 0.25)),
            "q50":  float(np.quantile(values, 0.50)),
            "q75":  float(np.quantile(values, 0.75)),
        },
    }


# Computes player-specific BAR thresholds using median; falls back to scaled population norm if a class is missing
def compute_bar_thresholds(
    values: np.ndarray,
    labels: np.ndarray,
    norms: Dict,
    min_scale: float,
) -> Dict[str, object]:
    center = float(np.median(values))
    scale  = float(np.quantile(values, 0.75) - np.quantile(values, 0.25))
    scale  = max(scale, min_scale)  # Prevent zero IQR

    focused_vals   = values[labels == 1]
    unfocused_vals = values[labels == 0]

    if focused_vals.size > 0:
        focused_threshold = float(np.median(focused_vals))
        fallback_focused  = False
    else:
        # Scale population norm by player's unfocused level relative to group
        scale_factor      = (float(np.median(unfocused_vals)) / norms["bar"]["unfocused_threshold"]) if unfocused_vals.size > 0 else 1.0
        focused_threshold = float(norms["bar"]["focused_threshold"]) * scale_factor
        fallback_focused  = True
        print(f"[Calibration] BAR: no focused windows — using scaled population norm: {focused_threshold:.4f} (scale={scale_factor:.4f})")

    if unfocused_vals.size > 0:
        unfocused_threshold = float(np.median(unfocused_vals))
        fallback_unfocused  = False
    else:
        # Scale population norm by player's focused level relative to group
        scale_factor        = (float(np.median(focused_vals)) / norms["bar"]["focused_threshold"]) if focused_vals.size > 0 else 1.0
        unfocused_threshold = float(norms["bar"]["unfocused_threshold"]) * scale_factor
        fallback_unfocused  = True
        print(f"[Calibration] BAR: no unfocused windows — using scaled population norm: {unfocused_threshold:.4f} (scale={scale_factor:.4f})")

    return {
        "focused_threshold":   focused_threshold,
        "unfocused_threshold": unfocused_threshold,
        "direction":           "higher_when_focused",
        "fallback_focused":    fallback_focused,    # True if population norm was used
        "fallback_unfocused":  fallback_unfocused,  # True if population norm was used
        "center": center,
        "scale":  scale,
        "summary": {
            "min":  float(np.min(values)),
            "max":  float(np.max(values)),
            "mean": float(np.mean(values)),        # kept for reference only
            "std":  float(np.std(values, ddof=0)), # kept for reference only
            "iqr":  float(np.quantile(values, 0.75) - np.quantile(values, 0.25)),
            "q25":  float(np.quantile(values, 0.25)),
            "q50":  float(np.quantile(values, 0.50)),
            "q75":  float(np.quantile(values, 0.75)),
        },
    }


# ---------------------------------------------------------------------------
# Calibration payload construction
# ---------------------------------------------------------------------------

# Assembles the full calibration JSON payload with TBR/BAR thresholds and session metadata
def build_calibration_payload(
    participant_id: str,
    tbr: np.ndarray,
    bar: np.ndarray,
    labels: np.ndarray,
    norms: Dict,
    cfg: CalibrationConfig,
) -> Dict[str, object]:

    # Count windows per label class for the session summary
    unique, counts = np.unique(labels, return_counts=True)
    label_summary = {str(int(k)): int(v) for k, v in zip(unique, counts)}

    tbr_thresholds = compute_tbr_thresholds(tbr, labels, norms, cfg.min_scale)
    bar_thresholds = compute_bar_thresholds(bar, labels, norms, cfg.min_scale)

    return {
        "participant_id": participant_id,
        "n_windows":      int(len(labels)),
        "label_summary":  label_summary,
        "tbr": tbr_thresholds,
        "bar": bar_thresholds,
        "notes": (
            "Calibration based on TBR and BAR only. "
            "Median and IQR are used as robust measures of central tendency and spread, "
            "as individual player sessions are short and susceptible to skew and outliers. "
            "TBR is higher during unfocused states (van Son et al., 2019). "
            "BAR is higher during focused states (Laureanti et al., 2021). "
            "Population-level fallback used when a focus state is missing."
        ),
    }


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main() -> None:
    cfg = CalibrationConfig(
        tutorial_features_path=base_dir() / "player_data" / "processed" / "current_player_features.csv",
        population_norms_path=base_dir() / "data" / "models" / "population_norms.json",
        output_path=base_dir() / "player_data" / "calibration" / "current_player_calibration.json",
        participant_id="current_player",
    )

    ensure_parent(cfg.output_path)

    norms        = load_population_norms(cfg.population_norms_path)
    df           = load_tutorial_data(cfg.tutorial_features_path)
    tbr, bar     = extract_ratios(df)
    labels       = derive_binary_labels(df)

    payload = build_calibration_payload(cfg.participant_id, tbr, bar, labels, norms, cfg)

    # Save calibration JSON for use by the inference server
    cfg.output_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(f"[OK] Calibration saved to: {cfg.output_path}")


if __name__ == "__main__":
    main()