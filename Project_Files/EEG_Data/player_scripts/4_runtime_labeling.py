from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Optional, Tuple

import numpy as np
import pandas as pd
from scipy.stats import norm


# All file paths used across the runtime labeling pipeline in one place
@dataclass(frozen=True)
class LabelingConfig:
    base_dir: Path
    master_features_path: Path
    raw_participants_root: Path
    out_dataset_path: Path
    out_sam_distribution_path: Path
    out_focus_distribution_path: Path
    out_behavior_report_path: Path
    sam_column_candidates: Tuple[str, ...] = ("sam_label", "sam_int", "sam_rating")  # Checked in order; first match wins


# Constructs default paths pointing to the current player's processed data
def build_default_config() -> LabelingConfig:
    base = Path(__file__).resolve().parent.parent
    return LabelingConfig(
        base_dir=base,
        master_features_path=base / "player_data" / "processed" / "current_player_features.csv",
        raw_participants_root=base / "player_data" / "current_player",
        out_dataset_path=base / "player_data" / "processed" / "current_player_labeled_features.csv",
        out_sam_distribution_path=base / "player_data" / "reports" / "report_sam.csv",
        out_focus_distribution_path=base / "player_data" / "reports" / "report_focus.csv",
        out_behavior_report_path=base / "player_data" / "reports" / "report_behavior.csv",
    )


# Creates all output directories if they don't already exist
def ensure_output_dirs(cfg: LabelingConfig) -> None:
    cfg.out_dataset_path.parent.mkdir(parents=True, exist_ok=True)
    cfg.out_sam_distribution_path.parent.mkdir(parents=True, exist_ok=True)
    cfg.out_behavior_report_path.parent.mkdir(parents=True, exist_ok=True)


# Returns the first matching column name from candidates; raises if none found
def pick_existing_column(df: pd.DataFrame, candidates: Tuple[str, ...], purpose: str) -> str:
    for name in candidates:
        if name in df.columns:
            return name
    raise ValueError(f"Missing {purpose} column. Expected one of: {list(candidates)}")


# Coerces a SAM series to integer; sets values outside 1–5 to NaN
def to_int_sam_1to5(series: pd.Series) -> pd.Series:
    s = pd.to_numeric(series, errors="coerce")
    s = s.where(s.between(1, 5))
    return s.astype("Int64")


# Log-linear correction to avoid 0 or 1 rates that would break norm.ppf in d' calculation
def safe_loglinear_rate(k: float, n: float) -> float:
    if not np.isfinite(k) or not np.isfinite(n) or n <= 0:
        return np.nan
    return float((k + 0.5) / (n + 1.0))  # log-linear correction for extreme proportions


# Computes d' (sensitivity index); clips rates to avoid norm.ppf returning ±inf
def safe_dprime(hr: float, far: float) -> float:
    if not (np.isfinite(hr) and np.isfinite(far)):
        return np.nan
    hr  = float(np.clip(hr,  1e-6, 1 - 1e-6))
    far = float(np.clip(far, 1e-6, 1 - 1e-6))
    return float(norm.ppf(hr) - norm.ppf(far))


# Loads current_player_features.csv and validates required columns
def load_master_features(cfg: LabelingConfig) -> pd.DataFrame:
    df = pd.read_csv(cfg.master_features_path, low_memory=False)

    required = {"participant", "start_time_s", "end_time_s"}
    missing = required - set(df.columns)
    if missing:
        raise ValueError(f"current_player_features.csv missing columns: {sorted(missing)}")

    df["participant"] = df["participant"].astype(str).str.strip()

    if "block" in df.columns:
        df["block"] = pd.to_numeric(df["block"], errors="coerce").astype("Int64")

    return df


# Detects the SAM column, cleans values, and adds a standardized `sam_label_final` column
def add_sam_label_final(df: pd.DataFrame, cfg: LabelingConfig) -> pd.DataFrame:
    sam_col = pick_existing_column(df, cfg.sam_column_candidates, purpose="SAM")
    out = df.copy()
    out["sam_label_final"] = to_int_sam_1to5(out[sam_col])

    before = len(out)
    out = out.dropna(subset=["sam_label_final"]).copy()
    out["sam_label_final"] = out["sam_label_final"].astype(int)

    print(f"SAM column: '{sam_col}' | Dropped invalid rows: {before - len(out):,}")
    return out


# Creates binary focus label: SAM 4–5 → focused (1), SAM 1–2 → unfocused (0); SAM=3 removed
def add_focus_label_binary(df: pd.DataFrame) -> pd.DataFrame:
    out = df.copy()
    # Exclude SAM=3 (ambiguous midpoint)
    out = out[out["sam_label_final"] != 3].copy()
    out["focus_label_binary"] = np.where(
        out["sam_label_final"].between(4, 5), 1, 0
    ).astype(int)
    return out


# Loads Behavior.csv for the current player; returns None if the file doesn't exist
def load_behavior_file(pid_dir: Path) -> Optional[pd.DataFrame]:
    path = pid_dir / "Behavior.csv"
    return pd.read_csv(path, low_memory=False) if path.exists() else None


# Filters the behavior DataFrame to only trial rows
def extract_trial_rows(behavior: pd.DataFrame) -> pd.DataFrame:
    if "row_type" not in behavior.columns:
        return pd.DataFrame()
    return behavior[behavior["row_type"].astype(str).str.strip().str.lower().eq("trial")].copy()


# Filters the behavior DataFrame to only summary rows (pre-aggregated block stats)
def extract_summary_rows(behavior: pd.DataFrame) -> pd.DataFrame:
    if "row_type" not in behavior.columns:
        return pd.DataFrame()
    return behavior[behavior["row_type"].astype(str).str.strip().str.lower().eq("summary")].copy()


# Standardizes column types across behavior DataFrames (block, excluded_fast, stimulus, outcome)
def normalize_behavior_columns(df: pd.DataFrame) -> pd.DataFrame:
    out = df.copy()
    if "block" in out.columns:
        out["block"] = pd.to_numeric(out["block"], errors="coerce").astype("Int64")
    out["excluded_fast"] = (
        pd.to_numeric(out.get("excluded_fast", 0), errors="coerce").fillna(0).astype(int)
    )
    for col in ("stimulus", "outcome"):
        if col not in out.columns:
            out[col] = ""
        out[col] = out[col].astype(str)
    return out


# Computes HR, FAR, and d' per block from raw trial rows; excludes fast-response trials
def compute_behavior_metrics_from_trials(trials: pd.DataFrame, cfg: LabelingConfig) -> pd.DataFrame:
    if trials.empty:
        return pd.DataFrame()

    trials = normalize_behavior_columns(trials)
    trials = trials[trials["excluded_fast"] == 0].copy()  # Remove too-fast trials
    if trials.empty:
        return pd.DataFrame()

    rows: list[Dict] = []
    for (participant, block), g in trials.groupby(["participant", "block"], dropna=False):
        stim = g["stimulus"].str.lower()
        outc = g["outcome"].str.lower()

        targets      = g[stim == "target"]
        nontargets   = g[stim == "nontarget"]  # matches after .lower() on 'nonTarget'

        hits         = float((outc.loc[targets.index]    == "hit").sum())
        false_alarms = float((outc.loc[nontargets.index] == "fa").sum())
        misses       = float((outc.loc[targets.index]    == "miss").sum())
        n_targets    = float(len(targets))
        n_nontargets = float(len(nontargets))

        hr  = safe_loglinear_rate(hits, n_targets)
        far = safe_loglinear_rate(false_alarms, n_nontargets)
        dp  = safe_dprime(hr, far)

        rows.append({
            "participant":      participant,
            "block":            int(block) if pd.notna(block) else np.nan,
            "beh_hits":         hits,
            "beh_false_alarms": false_alarms,
            "beh_misses":       misses,
            "beh_hr":           hr,
            "beh_far":          far,
            "beh_dprime":       dp,
            "beh_n_targets":    n_targets,
            "beh_n_nontargets": n_nontargets,
            "behavior_source":  "trial_rows",
        })
    return pd.DataFrame(rows)


# Fallback: computes HR, FAR, and d' from pre-aggregated summary rows when trial rows are unavailable
def compute_behavior_metrics_from_summary(summary: pd.DataFrame, cfg: LabelingConfig) -> pd.DataFrame:
    if summary.empty:
        return pd.DataFrame()

    summary = summary.copy()
    if "block" in summary.columns:
        summary["block"] = pd.to_numeric(summary["block"], errors="coerce").astype("Int64")

    rows: list[Dict] = []
    for (participant, block), g in summary.groupby(["participant", "block"], dropna=False):
        row = g.iloc[0]
        hr  = pd.to_numeric(row.get("hit_rate",         np.nan), errors="coerce")
        far = pd.to_numeric(row.get("false_alarm_rate", np.nan), errors="coerce")
        dp  = safe_dprime(float(hr), float(far))
        rows.append({
            "participant":       participant,
            "block":             int(block) if pd.notna(block) else np.nan,
            "beh_hr":            float(hr),
            "beh_far":           float(far),
            "beh_dprime":        dp,
            "beh_rt_mean_ms":    pd.to_numeric(row.get("rt_mean_ms",    np.nan), errors="coerce"),
            "beh_rtv_ms":        pd.to_numeric(row.get("rtv_ms",        np.nan), errors="coerce"),
            "beh_excluded_fast": pd.to_numeric(row.get("excluded_fast", np.nan), errors="coerce"),
            "behavior_source":   "summary_rows",
        })
    return pd.DataFrame(rows)


# Loads behavioral metrics for the current player; prefers trial rows over summary rows
def load_behavioral_metrics(cfg: LabelingConfig) -> pd.DataFrame:
    behavior = load_behavior_file(cfg.raw_participants_root)
    if behavior is None:
        return pd.DataFrame()

    behavior["participant"] = "current_player"

    trials  = extract_trial_rows(behavior)
    summary = extract_summary_rows(behavior)

    trials_df  = trials.copy()  if not trials.empty  else pd.DataFrame()
    summary_df = summary.copy() if not summary.empty else pd.DataFrame()

    if not trials_df.empty:
        trials_df["participant"] = "current_player"
    if not summary_df.empty:
        summary_df["participant"] = "current_player"

    # Use trial-level metrics if available; otherwise fall back to summary rows
    beh = compute_behavior_metrics_from_trials(trials_df, cfg)
    if not beh.empty:
        return beh
    return compute_behavior_metrics_from_summary(summary_df, cfg)


# Saves SAM label frequency counts to a CSV report
def save_sam_distribution(df: pd.DataFrame, path: Path) -> None:
    (df["sam_label_final"].value_counts().sort_index()
     .rename_axis("sam_label_final").reset_index(name="count")
     .to_csv(path, index=False))


# Saves binary focus label frequency counts to a CSV report
def save_focus_distribution(df: pd.DataFrame, path: Path) -> None:
    (df["focus_label_binary"].value_counts().sort_index()
     .rename_axis("focus_label_binary").reset_index(name="count")
     .to_csv(path, index=False))


# Main pipeline: load features → add labels → merge behavior → save outputs
def run_labeling_pipeline(cfg: LabelingConfig) -> None:
    print("=== Step 4: Runtime Labeling (current player) ===")

    master  = load_master_features(cfg)
    labeled = add_sam_label_final(master, cfg)
    labeled = add_focus_label_binary(labeled)

    print("Loading behavioral metrics...")
    beh = load_behavioral_metrics(cfg)

    if beh.empty:
        print("[Info] No behavioral data found.")
        labeled["behavior_source"] = "none"
    elif "block" in labeled.columns:
        # Merge behavioral metrics onto the labeled features by participant and block
        labeled = labeled.merge(beh, on=["participant", "block"], how="left")
        beh.sort_values(["participant", "block"]).to_csv(cfg.out_behavior_report_path, index=False)
        print(f"[Saved] {cfg.out_behavior_report_path}")
    else:
        print("[Info] No block column — behavioral metrics not merged.")
        labeled["behavior_source"] = "none"

    labeled.to_csv(cfg.out_dataset_path, index=False)
    print(f"[Saved] dataset: {cfg.out_dataset_path}")

    save_sam_distribution(labeled, cfg.out_sam_distribution_path)
    save_focus_distribution(labeled, cfg.out_focus_distribution_path)
    print("=== Done ===")


def main() -> None:
    cfg = build_default_config()
    ensure_output_dirs(cfg)
    run_labeling_pipeline(cfg)


if __name__ == "__main__":
    main()