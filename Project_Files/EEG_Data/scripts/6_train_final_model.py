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


# Config for final model training — mirrors classify.py settings for consistency
@dataclass(frozen=True)
class Config:
    dataset_path: Path
    participant_col: str = "participant"
    group_col: str = "participant"
    label_col: str = "focus_label_binary"  # Binary target: 0 = not focused, 1 = focused
    #weight_col: str = "sample_weight"
    use_pca: bool = True
    pca_variance: float = 0.95             # Retain 95% of explained variance
    random_state: int = 42
    model_name: str = "RF"                 # Best-performing model from cross-validation
    model_path: Path = Path("data/models/original_focus_model.joblib")
    feature_columns_path: Path = Path("data/models/feature_columns.json")   # Saved for inference alignment
    metadata_path: Path = Path("data/models/training_metadata.json")        # Provenance record


def ensure_parent(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


# Selects EEG band-power columns by prefix and drops any that are entirely non-numeric
def select_feature_columns(df: pd.DataFrame, cfg: Config) -> List[str]:
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


# Builds the preprocessing pipeline: median imputation → z-score scaling → optional PCA
def make_preprocessor(feature_cols: List[str], cfg: Config) -> ColumnTransformer:
    steps: List[tuple[str, BaseEstimator]] = [
        ("imputer", SimpleImputer(strategy="median")),
        ("scaler", StandardScaler()),
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


# Builds a metadata dict for provenance: records dataset stats, config choices, and model rationale
def build_metadata(
    df: pd.DataFrame,
    feature_cols: List[str],
    cfg: Config,
) -> Dict[str, Any]:
    label_counts = df[cfg.label_col].value_counts(dropna=False).sort_index().to_dict()
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


def main() -> None:
    base_dir = Path(__file__).resolve().parent.parent
    cfg = Config(dataset_path=base_dir / "data" / "datasets" / "dataset_final.csv")

    model_path           = base_dir / cfg.model_path
    feature_columns_path = base_dir / cfg.feature_columns_path
    metadata_path        = base_dir / cfg.metadata_path

    ensure_parent(model_path)
    ensure_parent(feature_columns_path)
    ensure_parent(metadata_path)

    # Load dataset and clean label column
    df = pd.read_csv(cfg.dataset_path, low_memory=False)
    df[cfg.participant_col] = df[cfg.participant_col].astype(str).str.strip()
    df[cfg.label_col]       = pd.to_numeric(df[cfg.label_col], errors="coerce")
    df = df.dropna(subset=[cfg.label_col]).copy()
    df[cfg.label_col] = df[cfg.label_col].astype(int)

    feature_cols = select_feature_columns(df, cfg)

    # Replace infinities with NaN so the imputer handles them uniformly
    X = df[feature_cols].apply(pd.to_numeric, errors="coerce").replace([np.inf, -np.inf], np.nan)
    y = df[cfg.label_col]

    # Train on the full dataset (no held-out test set — evaluation was done in Step 5 via CV)
    model = build_model(feature_cols, cfg)
    model.fit(X, y)

    metadata = build_metadata(df, feature_cols, cfg)

    # Persist model, feature list, and metadata for deployment / reproducibility
    joblib.dump(model, model_path)
    feature_columns_path.write_text(json.dumps(feature_cols, indent=2), encoding="utf-8")
    metadata_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")

    print(f"[OK] Saved model to: {model_path}")
    print(f"[OK] Saved feature columns to: {feature_columns_path}")
    print(f"[OK] Saved training metadata to: {metadata_path}")


if __name__ == "__main__":
    main()