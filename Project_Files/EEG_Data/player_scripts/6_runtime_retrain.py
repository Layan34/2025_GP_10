from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, List

import json
import joblib
import numpy as np
import pandas as pd

from sklearn.base import BaseEstimator
from sklearn.compose import ColumnTransformer
from sklearn.decomposition import PCA
from sklearn.ensemble import RandomForestClassifier
from sklearn.impute import SimpleImputer
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler


# ---------------------------------------------------------------------------
# Configuration — mirrors 6_train_final_model.py exactly
# ---------------------------------------------------------------------------

# Settings must stay in sync with the original training script to ensure consistency
@dataclass(frozen=True)
class Config:
    participant_col: str  = "participant"
    group_col:       str  = "participant"
    label_col:       str  = "focus_label_binary"  # Binary target: 0 = unfocused, 1 = focused
    use_pca:         bool = True
    pca_variance:    float = 0.95                  # Retain 95% of explained variance
    random_state:    int  = 42
    model_name:      str  = "RF"


# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------

def base_dir() -> Path:
    return Path(__file__).resolve().parent.parent

# Training dataset from the original participants (P1–P9)
def dataset_path() -> Path:
    return base_dir() / "data" / "datasets" / "dataset_final.csv"

# Current player's labeled feature file produced by Step 4
def new_player_path() -> Path:
    return base_dir() / "player_data" / "processed" / "current_player_labeled_features.csv"

# Output path for the updated model (original_focus_model.joblib is never overwritten)
def model_path() -> Path:
    return base_dir() / "data" / "models" / "final_focus_model.joblib"

def feature_columns_path() -> Path:
    return base_dir() / "data" / "models" / "feature_columns.json"

def metadata_path() -> Path:
    return base_dir() / "data" / "models" / "training_metadata.json"

def ensure_parent(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


# ---------------------------------------------------------------------------
# Feature selection — mirrors 6_train_final_model.py exactly
# ---------------------------------------------------------------------------

# Selects EEG band-power columns by prefix; drops any that are entirely non-numeric
def select_feature_columns(df: pd.DataFrame) -> List[str]:
    allowed_prefixes = (
        "theta_",
        "alpha_",
        "beta_",
        "tbr_",
        "bar_",
    )

    feature_cols = [
        col for col in df.columns
        if col.startswith(allowed_prefixes)
    ]

    if not feature_cols:
        raise ValueError(
            "No EEG feature columns found. Expected columns starting with: "
            f"{allowed_prefixes}"
        )

    usable_cols: List[str] = []
    for col in feature_cols:
        numeric_series = pd.to_numeric(df[col], errors="coerce")
        if numeric_series.notna().any():  # Keep only columns with at least one valid value
            usable_cols.append(col)

    if not usable_cols:
        raise ValueError("No usable numeric EEG feature columns found.")

    return sorted(usable_cols)


# ---------------------------------------------------------------------------
# Model building — mirrors 6_train_final_model.py exactly
# ---------------------------------------------------------------------------

# Builds the preprocessing pipeline: median imputation → z-score scaling → optional PCA
def make_preprocessor(feature_cols: List[str], cfg: Config) -> ColumnTransformer:
    steps: List[tuple[str, BaseEstimator]] = [
        ("imputer", SimpleImputer(strategy="median")),
        ("scaler",  StandardScaler()),
    ]
    if cfg.use_pca:
        steps.append(("pca", PCA(n_components=cfg.pca_variance, random_state=cfg.random_state)))

    numeric_pipe = Pipeline(steps)
    return ColumnTransformer([("num", numeric_pipe, feature_cols)], remainder="drop")


# Wraps the preprocessor and Random Forest classifier into a single sklearn Pipeline
def build_model(feature_cols: List[str], cfg: Config) -> Pipeline:
    preprocessor = make_preprocessor(feature_cols, cfg)
    return Pipeline(
        steps=[
            ("pre", preprocessor),
            ("clf", RandomForestClassifier(
                n_estimators=300,
                class_weight="balanced_subsample",  # Handles class imbalance per tree
                random_state=cfg.random_state,
                n_jobs=-1,
            )),
        ]
    )


# ---------------------------------------------------------------------------
# Metadata — mirrors 6_train_final_model.py exactly
# ---------------------------------------------------------------------------

# Builds a provenance record capturing dataset stats and model configuration
def build_metadata(
    df: pd.DataFrame,
    feature_cols: List[str],
    cfg: Config,
) -> Dict[str, Any]:
    label_counts       = df[cfg.label_col].value_counts(dropna=False).sort_index().to_dict()
    participant_counts = df[cfg.group_col].astype(str).nunique()

    return {
        "model_name":         cfg.model_name,
        "label_column":       cfg.label_col,
        "participant_column": cfg.participant_col,
        "group_column":       cfg.group_col,
        "random_state":       cfg.random_state,
        "use_pca":            cfg.use_pca,
        "pca_variance":       cfg.pca_variance,
        "n_rows":             int(len(df)),
        "n_participants":     int(participant_counts),
        "n_features":         int(len(feature_cols)),
        "feature_columns":    feature_cols,
        "label_counts":       {str(k): int(v) for k, v in label_counts.items()},
        "reason_for_choice": (
            "RF was selected because it achieved the best balanced accuracy "
            "across all classifiers in the cross-validation evaluation, making it "
            "the most suitable model for robust cross-participant focus detection."
        ),
    }


# ---------------------------------------------------------------------------
# Main retrain flow
# ---------------------------------------------------------------------------

def retrain() -> None:
    cfg = Config()

    # ── 1. Load original dataset (p1-p9) ────────────────────────────────────
    if not dataset_path().exists():
        raise FileNotFoundError(f"Missing original dataset: {dataset_path()}")

    original_df = pd.read_csv(dataset_path(), low_memory=False)
    print(f"  [Info] Original dataset: {len(original_df):,} rows")

    # ── 2. Load current player labeled features ──────────────────────────────
    if not new_player_path().exists():
        raise FileNotFoundError(f"Missing player features: {new_player_path()}")

    player_df = pd.read_csv(new_player_path(), low_memory=False)
    print(f"  [Info] Current player rows: {len(player_df):,}")

    # ── 3. Combine original + current player ─────────────────────────────────
    combined_df = pd.concat([original_df, player_df], ignore_index=True)
    combined_df[cfg.participant_col] = combined_df[cfg.participant_col].astype(str).str.strip()
    combined_df[cfg.label_col]       = pd.to_numeric(combined_df[cfg.label_col], errors="coerce")
    combined_df = combined_df.dropna(subset=[cfg.label_col]).copy()
    combined_df[cfg.label_col] = combined_df[cfg.label_col].astype(int)

    print(f"  [Info] Combined: {len(combined_df):,} rows | participants={combined_df[cfg.participant_col].nunique()}")

    # ── 4. Select features ───────────────────────────────────────────────────
    feature_cols = select_feature_columns(combined_df)
    # Replace infinities with NaN so the imputer handles them uniformly
    X = combined_df[feature_cols].apply(pd.to_numeric, errors="coerce").replace([np.inf, -np.inf], np.nan)
    y = combined_df[cfg.label_col]

    # ── 5. Build and train model from scratch ────────────────────────────────
    model = build_model(feature_cols, cfg)
    model.fit(X, y)
    print(f"  [Info] Model trained from scratch on {len(combined_df):,} rows")

    # ── 6. Build metadata ────────────────────────────────────────────────────
    metadata = build_metadata(combined_df, feature_cols, cfg)

    # ── 7. Save — original_focus_model.joblib is never touched ──────────────
    ensure_parent(model_path())
    ensure_parent(feature_columns_path())
    ensure_parent(metadata_path())

    joblib.dump(model, model_path())
    feature_columns_path().write_text(json.dumps(feature_cols, indent=2), encoding="utf-8")
    metadata_path().write_text(json.dumps(metadata, indent=2), encoding="utf-8")

    print(f"[OK] Player model saved : {model_path().name}")
    print(f"[OK] Feature columns    : {feature_columns_path().name}")
    print(f"[OK] Training metadata  : {metadata_path().name}")


def main() -> None:
    print("=== Runtime Retrain ===")
    try:
        retrain()
    except Exception as exc:
        print(f"[Fail] Retrain: {exc}")
        raise
    print("=== Done ===")


if __name__ == "__main__":
    main()