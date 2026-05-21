from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import numpy as np
import pandas as pd
from scipy.stats import norm


# All paths and settings for the behavioral validation step
@dataclass(frozen=True)
class BehaviorValidationConfig:
    base_dir: Path
    dataset_final_path: Path
    raw_participants_root: Path
    output_by_block_path: Path    # Per participant-block behavioral metrics merged with labels
    output_summary_path: Path     # Averaged metrics grouped by focus label and SAM rating
    dprime_clip: float = 3.5      # Cap d' to avoid extreme values from near-perfect performance


# Builds default paths relative to the project root
def build_default_config() -> BehaviorValidationConfig:
    base_dir = Path(__file__).resolve().parent.parent
    reports_dir = base_dir / "data" / "reports"

    return BehaviorValidationConfig(
        base_dir=base_dir,
        dataset_final_path=base_dir / "data" / "datasets" / "dataset_final.csv",
        raw_participants_root=base_dir / "data" / "raw",
        output_by_block_path=reports_dir / "behavior_validation_by_block.csv",
        output_summary_path=reports_dir / "behavior_validation_summary.csv",
    )


def ensure_output_dir(cfg: BehaviorValidationConfig) -> None:
    cfg.output_by_block_path.parent.mkdir(parents=True, exist_ok=True)


# Log-linear correction to avoid 0 or 1 rates that would break norm.ppf in d' calculation
def safe_loglinear_rate(k: float, n: float) -> float:
    if not np.isfinite(k) or not np.isfinite(n) or n <= 0:
        return np.nan
    return float((k + 0.5) / (n + 1.0))


# Computes d' (sensitivity index); clips result to ±dprime_clip to handle extreme rates
def safe_dprime(hit_rate: float, false_alarm_rate: float, clip: float) -> float:
    if not (np.isfinite(hit_rate) and np.isfinite(false_alarm_rate)):
        return np.nan

    hit_rate = float(np.clip(hit_rate, 1e-6, 1 - 1e-6))
    false_alarm_rate = float(np.clip(false_alarm_rate, 1e-6, 1 - 1e-6))

    dprime = float(norm.ppf(hit_rate) - norm.ppf(false_alarm_rate))
    return float(np.clip(dprime, -clip, clip))


# Loads SAM and focus labels from dataset_final.csv, collapsed to one row per participant-block
def load_dataset_labels(cfg: BehaviorValidationConfig) -> pd.DataFrame:
    df = pd.read_csv(cfg.dataset_final_path, low_memory=False)

    required_columns = ["participant", "block", "sam_label", "focus_label_binary"]
    missing_columns = [col for col in required_columns if col not in df.columns]
    if missing_columns:
        raise ValueError(f"dataset_final.csv missing required columns: {missing_columns}")

    labels_df = df[required_columns].copy()
    labels_df["participant"] = labels_df["participant"].astype(str).str.strip()
    labels_df["block"] = pd.to_numeric(labels_df["block"], errors="coerce")
    labels_df["sam_label"] = pd.to_numeric(labels_df["sam_label"], errors="coerce")
    labels_df["focus_label_binary"] = pd.to_numeric(labels_df["focus_label_binary"], errors="coerce")

    labels_df = labels_df.dropna(subset=["participant", "block", "sam_label", "focus_label_binary"]).copy()
    labels_df["block"] = labels_df["block"].astype(int)
    labels_df["sam_label"] = labels_df["sam_label"].astype(int)
    labels_df["focus_label_binary"] = labels_df["focus_label_binary"].astype(int)

    # Deduplicate: keep one label row per participant-block (dataset has sample-level rows)
    labels_df = (
        labels_df.groupby(["participant", "block"], as_index=False)
        .agg(
            sam_label=("sam_label", "first"),
            focus_label_binary=("focus_label_binary", "first"),
        )
    )

    if labels_df.empty:
        raise ValueError("No valid participant-block labels found in dataset_final.csv.")

    return labels_df


# Returns participant directories sorted numerically (P1, P2, ... P10, not P1, P10, P2)
def list_participant_dirs(raw_root: Path) -> list[Path]:
    participant_dirs = [path for path in raw_root.glob("P*") if path.is_dir()]
    participant_dirs.sort(key=lambda path: int(path.name[1:]))
    return participant_dirs


# Loads and cleans trial rows from Behavior.csv; removes fast-excluded trials if flagged
def load_trial_rows(behavior_path: Path) -> pd.DataFrame:
    behavior = pd.read_csv(behavior_path, low_memory=False)

    required_columns = ["row_type", "block", "stimulus", "outcome"]
    missing_columns = [col for col in required_columns if col not in behavior.columns]
    if missing_columns:
        raise ValueError(f"{behavior_path.name} missing required columns: {missing_columns}")

    trial_rows = behavior[
        behavior["row_type"].astype(str).str.strip().str.lower().eq("trial")
    ].copy()

    if trial_rows.empty:
        return pd.DataFrame()

    trial_rows["block"] = pd.to_numeric(trial_rows["block"], errors="coerce")
    trial_rows = trial_rows.dropna(subset=["block"]).copy()
    trial_rows["block"] = trial_rows["block"].astype(int)

    trial_rows["stimulus"] = trial_rows["stimulus"].astype(str).str.strip().str.lower()
    trial_rows["outcome"] = trial_rows["outcome"].astype(str).str.strip().str.lower()

    if "excluded_fast" in trial_rows.columns:
        trial_rows["excluded_fast"] = pd.to_numeric(trial_rows["excluded_fast"], errors="coerce").fillna(0).astype(int)
        trial_rows = trial_rows[trial_rows["excluded_fast"] == 0].copy()  # Remove too-fast responses
    else:
        trial_rows["excluded_fast"] = 0

    if "rt_ms" in trial_rows.columns:
        trial_rows["rt_ms"] = pd.to_numeric(trial_rows["rt_ms"], errors="coerce")
    else:
        trial_rows["rt_ms"] = np.nan

    return trial_rows


# Computes hits, misses, FA, CR, HR, FAR, accuracy, d', and RT stats per block for one participant
def compute_behavior_metrics_for_participant(
    participant_id: str,
    trial_rows: pd.DataFrame,
    cfg: BehaviorValidationConfig,
) -> pd.DataFrame:
    if trial_rows.empty:
        return pd.DataFrame()

    rows: list[dict] = []

    for block, group in trial_rows.groupby("block", sort=True):
        targets = group[group["stimulus"] == "target"]
        nontargets = group[group["stimulus"] == "nontarget"]

        n_targets = float(len(targets))
        n_nontargets = float(len(nontargets))

        hits = float((targets["outcome"] == "hit").sum())
        misses = float((targets["outcome"] == "miss").sum())
        false_alarms = float((nontargets["outcome"] == "fa").sum())
        correct_rejections = float((nontargets["outcome"].isin(["cr", "correct_rejection"])).sum())

        hit_rate = safe_loglinear_rate(hits, n_targets)
        false_alarm_rate = safe_loglinear_rate(false_alarms, n_nontargets)
        dprime = safe_dprime(hit_rate, false_alarm_rate, cfg.dprime_clip)

        total_trials = n_targets + n_nontargets
        correct_trials = hits + correct_rejections
        accuracy = float(correct_trials / total_trials) if total_trials > 0 else np.nan

        # RT stats computed only over hit trials (response was made)
        hit_rt_values = targets.loc[targets["outcome"] == "hit", "rt_ms"].dropna()
        mean_hit_rt_ms = float(hit_rt_values.mean()) if not hit_rt_values.empty else np.nan
        std_hit_rt_ms = float(hit_rt_values.std(ddof=0)) if not hit_rt_values.empty else np.nan  # Population std

        rows.append(
            {
                "participant": participant_id,
                "block": int(block),
                "beh_hits": hits,
                "beh_misses": misses,
                "beh_false_alarms": false_alarms,
                "beh_correct_rejections": correct_rejections,
                "beh_n_targets": n_targets,
                "beh_n_nontargets": n_nontargets,
                "beh_hit_rate": hit_rate,
                "beh_false_alarm_rate": false_alarm_rate,
                "beh_accuracy": accuracy,
                "beh_dprime": dprime,
                "beh_mean_hit_rt_ms": mean_hit_rt_ms,
                "beh_std_hit_rt_ms": std_hit_rt_ms,
            }
        )

    return pd.DataFrame(rows)


# Iterates over all participant directories and aggregates their behavioral metrics
def load_behavior_metrics(cfg: BehaviorValidationConfig) -> pd.DataFrame:
    all_metrics: list[pd.DataFrame] = []

    for participant_dir in list_participant_dirs(cfg.raw_participants_root):
        behavior_path = participant_dir / "Behavior.csv"
        if not behavior_path.exists():
            continue

        try:
            trial_rows = load_trial_rows(behavior_path)
            participant_metrics = compute_behavior_metrics_for_participant(
                participant_id=participant_dir.name,
                trial_rows=trial_rows,
                cfg=cfg,
            )
            if not participant_metrics.empty:
                all_metrics.append(participant_metrics)
        except Exception as exc:
            print(f"[Fail] {participant_dir.name}: {exc}")

    if not all_metrics:
        raise ValueError("No behavioral metrics could be computed from Behavior.csv files.")

    behavior_df = pd.concat(all_metrics, ignore_index=True)
    behavior_df = behavior_df.sort_values(["participant", "block"]).reset_index(drop=True)
    return behavior_df


# Builds a summary table: mean behavioral metrics grouped by focus label and SAM rating
def build_validation_summary(merged_df: pd.DataFrame) -> pd.DataFrame:
    metric_columns = [
        "beh_hit_rate",
        "beh_false_alarm_rate",
        "beh_accuracy",
        "beh_dprime",
        "beh_mean_hit_rt_ms",
        "beh_std_hit_rt_ms",
    ]

    summary = (
        merged_df.groupby(["focus_label_binary", "sam_label"], as_index=False)[metric_columns]
        .mean(numeric_only=True)
    )

    # Add block counts per group for context
    counts = (
        merged_df.groupby(["focus_label_binary", "sam_label"], as_index=False)
        .size()
        .rename(columns={"size": "n_blocks"})
    )

    summary = summary.merge(counts, on=["focus_label_binary", "sam_label"], how="left")
    summary = summary.sort_values(["focus_label_binary", "sam_label"]).reset_index(drop=True)
    return summary


# Main pipeline: load labels → compute behavior metrics → merge → save reports
def run_behavior_validation(cfg: BehaviorValidationConfig) -> None:
    print("=== Behavioral Validation ===")

    labels_df = load_dataset_labels(cfg)
    behavior_df = load_behavior_metrics(cfg)

    # Inner join: only keep blocks present in both the EEG labels and behavioral data
    merged_df = labels_df.merge(
        behavior_df,
        on=["participant", "block"],
        how="inner",
    )

    if merged_df.empty:
        raise RuntimeError("No matching participant-block pairs were found between labels and behavioral data.")

    summary_df = build_validation_summary(merged_df)

    merged_df.to_csv(cfg.output_by_block_path, index=False)
    summary_df.to_csv(cfg.output_summary_path, index=False)

    print(f"[OK] Saved block-level validation: {cfg.output_by_block_path}")
    print(f"[OK] Saved summary validation: {cfg.output_summary_path}")
    print(f"[OK] Matched blocks: {len(merged_df):,}")


def main() -> None:
    cfg = build_default_config()
    ensure_output_dir(cfg)
    run_behavior_validation(cfg)


if __name__ == "__main__":
    main()