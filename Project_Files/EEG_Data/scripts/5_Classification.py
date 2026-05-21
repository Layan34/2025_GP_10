from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Tuple

import numpy as np
import pandas as pd
from sklearn.base import BaseEstimator, ClassifierMixin, clone
from sklearn.compose import ColumnTransformer
from sklearn.decomposition import PCA
from sklearn.ensemble import HistGradientBoostingClassifier, RandomForestClassifier
from sklearn.impute import SimpleImputer
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import (
    accuracy_score,
    balanced_accuracy_score,
    classification_report,
    confusion_matrix,
    f1_score,
    precision_score,
    recall_score,
)
from sklearn.model_selection import StratifiedGroupKFold
from sklearn.neighbors import KNeighborsClassifier
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler
from sklearn.svm import LinearSVC, SVC
from xgboost import XGBClassifier

import torch
import torch.nn as nn
from torch.utils.data import DataLoader, TensorDataset


# ──────────────────────────────────────────────────────────────────────────────
# Config
# ──────────────────────────────────────────────────────────────────────────────

# Holds all experiment settings in one place; frozen so nothing can mutate it at runtime.
@dataclass(frozen=True)
class Config:
    dataset_path: Path
    participant_col: str = "participant"
    group_col: str = "participant"          # Column used to keep participants together during CV splits
    label_col: str = "focus_label_binary"  # Binary target: 0 = not focused, 1 = focused
    use_pca: bool = True
    pca_variance: float = 0.95             # Retain 95% of explained variance
    random_state: int = 42
    outer_splits: int = 10                 # Number of folds in the outer CV loop
    out_detailed: Path = Path("data/results/classification_detailed.csv")
    out_summary: Path = Path("data/results/classification_summary.csv")
    out_report: Path = Path("data/results/classification_report.txt")

    # Only columns whose names start with these prefixes are treated as EEG features
    allowed_feature_prefixes: Tuple[str, ...] = (
        "theta_",
        "alpha_",
        "beta_",
        "tbr_",   # Theta/Beta ratio
        "bar_",   # Beta/Alpha ratio
    )

    # Deep-learning training hyper-parameters
    dl_epochs: int = 50
    dl_batch_size: int = 64
    dl_lr: float = 1e-3
    dl_patience: int = 10   # Stop early if validation loss doesn't improve for this many epochs


def ensure_parent_dir(path: Path) -> None:
    """Create all missing parent directories for the given file path."""
    path.parent.mkdir(parents=True, exist_ok=True)


# ──────────────────────────────────────────────────────────────────────────────
# Data helpers
# ──────────────────────────────────────────────────────────────────────────────

def load_dataset(path: Path) -> pd.DataFrame:
    """Load the CSV, validate required columns, and clean the label column."""
    df = pd.read_csv(path, low_memory=False)
    for col in ("participant", "focus_label_binary"):
        if col not in df.columns:
            raise ValueError(f"Missing required column: {col}")
    df["participant"] = df["participant"].astype(str).str.strip()
    df["focus_label_binary"] = pd.to_numeric(df["focus_label_binary"], errors="coerce")
    # Drop rows where participant ID or label is missing
    df = df.dropna(subset=["participant", "focus_label_binary"]).copy()
    df["focus_label_binary"] = df["focus_label_binary"].astype(int)
    found = set(df["focus_label_binary"].unique())
    if not found.issubset({0, 1}):
        raise ValueError(f"Label column must contain only 0/1, found: {sorted(found)}")
    if df.empty:
        raise ValueError("Dataset is empty after cleaning.")
    return df


def select_eeg_feature_columns(df: pd.DataFrame, cfg: Config) -> List[str]:
    """Return sorted list of numeric EEG feature column names based on allowed prefixes."""
    feature_cols = [c for c in df.columns if c.startswith(cfg.allowed_feature_prefixes)]
    if not feature_cols:
        raise ValueError(f"No EEG feature columns found with prefixes: {cfg.allowed_feature_prefixes}")
    # Keep only columns that have at least one non-NaN numeric value
    usable = [c for c in feature_cols if pd.to_numeric(df[c], errors="coerce").notna().any()]
    if not usable:
        raise ValueError("No usable numeric EEG feature columns found.")
    return sorted(usable)


# ──────────────────────────────────────────────────────────────────────────────
# Preprocessors
# ──────────────────────────────────────────────────────────────────────────────

def make_preprocessor(feature_cols: List[str], cfg: Config) -> ColumnTransformer:
    """Build a pipeline: median imputation → z-score scaling → optional PCA."""
    steps: List[Tuple[str, BaseEstimator]] = [
        ("imputer", SimpleImputer(strategy="median")),
        ("scaler", StandardScaler()),
    ]
    if cfg.use_pca:
        steps.append(("pca", PCA(n_components=cfg.pca_variance, random_state=cfg.random_state)))
    return ColumnTransformer(
        transformers=[("num", Pipeline(steps=steps), feature_cols)],
        remainder="drop",  # Silently discard all non-EEG columns
    )


def make_preprocessor_no_pca(feature_cols: List[str]) -> ColumnTransformer:
    """Same as make_preprocessor but without PCA — used for DL models that expect raw dimensions."""
    steps: List[Tuple[str, BaseEstimator]] = [
        ("imputer", SimpleImputer(strategy="median")),
        ("scaler", StandardScaler()),
    ]
    return ColumnTransformer(
        transformers=[("num", Pipeline(steps=steps), feature_cols)],
        remainder="drop",
    )


# ──────────────────────────────────────────────────────────────────────────────
# Deep-learning models  (EEG-adapted, 1-D)
# ──────────────────────────────────────────────────────────────────────────────

# ── plain MLP (unchanged) ────────────────────────────────────────────────────

class _MLP(nn.Module):
    """Fully-connected MLP with BatchNorm and Dropout for regularization."""
    def __init__(self, in_dim: int, hidden: Tuple[int, ...] = (256, 128, 64)):
        super().__init__()
        layers: List[nn.Module] = []
        prev = in_dim
        for h in hidden:
            layers += [nn.Linear(prev, h), nn.BatchNorm1d(h), nn.ReLU(), nn.Dropout(0.3)]
            prev = h
        layers.append(nn.Linear(prev, 1))  # Single logit output for binary classification
        self.net = nn.Sequential(*layers)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return self.net(x).squeeze(1)


# ── EEG-VGG16  (1-D version of VGG-16 block structure) ───────────────────────

class _EEG_VGG16(nn.Module):
    """
    1-D VGG-16-style network for EEG band-power feature vectors.

    The number of pooling stages is automatically capped so that the
    spatial dimension never collapses to zero — safe for small feature
    vectors (e.g. 30–200 features after PCA is skipped).
    AdaptiveAvgPool1d(1) at the end makes the classifier size-agnostic.
    """

    def __init__(self, in_len: int):
        super().__init__()

        # VGG-16 stage layout: (in_channels, out_channels, num_convs)
        # Stages are skipped if the input length is too short to pool.
        stage_cfgs = [
            (1,   64,  2),   # stage 1 — VGG conv1_x
            (64,  128, 2),   # stage 2 — VGG conv2_x
            (128, 256, 3),   # stage 3 — VGG conv3_x
            (256, 512, 3),   # stage 4 — VGG conv4_x
            (512, 512, 3),   # stage 5 — VGG conv5_x
        ]

        cur_len = in_len
        stages  = []
        last_ch = 1
        for in_c, out_c, n_conv in stage_cfgs:
            if cur_len < 2:          # can't pool any further — stop here
                break
            layers = []
            for i in range(n_conv):
                layers += [
                    nn.Conv1d(in_c if i == 0 else out_c, out_c,
                              kernel_size=3, padding=1),
                    nn.BatchNorm1d(out_c),
                    nn.ReLU(inplace=True),
                ]
            layers.append(nn.MaxPool1d(kernel_size=2, stride=2))
            stages.append(nn.Sequential(*layers))
            cur_len  = cur_len // 2
            last_ch  = out_c

        self.features      = nn.Sequential(*stages)
        self.adaptive_pool = nn.AdaptiveAvgPool1d(1)   # always → (B, last_ch, 1)

        # Three-layer classifier head; sizes shrink gracefully for small inputs
        self.classifier = nn.Sequential(
            nn.Linear(last_ch, min(256, last_ch)),
            nn.ReLU(inplace=True),
            nn.Dropout(0.5),
            nn.Linear(min(256, last_ch), min(128, last_ch)),
            nn.ReLU(inplace=True),
            nn.Dropout(0.5),
            nn.Linear(min(128, last_ch), 1),
        )

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        x = x.unsqueeze(1)                        # (B, n_features) → (B, 1, n_features)
        x = self.features(x)
        x = self.adaptive_pool(x).squeeze(-1)     # (B, last_ch)
        return self.classifier(x).squeeze(1)


# ── EEG-ResNet  (1-D version of ResNet-18/34 residual block structure) ────────

class _ResBlock1D(nn.Module):
    """Basic 1-D residual block: two Conv1d layers with a skip (identity) connection."""

    def __init__(self, channels: int, stride: int = 1):
        super().__init__()
        self.conv1 = nn.Conv1d(channels, channels, kernel_size=3,
                               stride=stride, padding=1, bias=False)
        self.bn1   = nn.BatchNorm1d(channels)
        self.relu  = nn.ReLU(inplace=True)
        self.conv2 = nn.Conv1d(channels, channels, kernel_size=3,
                               padding=1, bias=False)
        self.bn2   = nn.BatchNorm1d(channels)

        # Downsample the skip connection when stride > 1 to match spatial size
        self.downsample = None
        if stride != 1:
            self.downsample = nn.Sequential(
                nn.Conv1d(channels, channels, kernel_size=1, stride=stride, bias=False),
                nn.BatchNorm1d(channels),
            )

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        identity = x
        out = self.relu(self.bn1(self.conv1(x)))
        out = self.bn2(self.conv2(out))
        if self.downsample is not None:
            identity = self.downsample(x)
        return self.relu(out + identity)   # Add residual before final activation


class _EEG_ResNet(nn.Module):
    """
    1-D ResNet-18-style network for EEG band-power feature vectors.

    Key differences from the image ResNet:
    - stem uses stride=1 + no MaxPool  →  preserves length for small inputs
    - _make_transition uses stride=2 only when length allows it (≥2)
    - AdaptiveAvgPool1d(1) makes the FC head size-agnostic
    """

    def __init__(self, in_len: int):
        super().__init__()

        # Stem: single conv, no pooling, to avoid shrinking tiny feature vectors
        self.stem = nn.Sequential(
            nn.Conv1d(1, 64, kernel_size=3, stride=1, padding=1, bias=False),
            nn.BatchNorm1d(64),
            nn.ReLU(inplace=True),
        )

        # Four residual layer groups mirroring ResNet-18 (2 blocks each)
        cur = in_len
        self.layer1 = nn.Sequential(_ResBlock1D(64), _ResBlock1D(64))
        self.layer2, cur = self._safe_layer(64,  128, cur)
        self.layer3, cur = self._safe_layer(128, 256, cur)
        self.layer4, cur = self._safe_layer(256, 512, cur)

        self.pool = nn.AdaptiveAvgPool1d(1)   # → (B, 512, 1) regardless of length
        self.fc   = nn.Linear(512, 1)

    @staticmethod
    def _safe_layer(in_c: int, out_c: int, cur_len: int):
        """Channel-transition block; uses stride=2 only when input length allows halving."""
        stride = 2 if cur_len >= 2 else 1
        transition = nn.Sequential(
            nn.Conv1d(in_c, out_c, kernel_size=1, stride=stride, bias=False),
            nn.BatchNorm1d(out_c),
            nn.ReLU(inplace=True),
        )
        layer = nn.Sequential(transition, _ResBlock1D(out_c))
        new_len = max(1, cur_len // stride)
        return layer, new_len

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        x = x.unsqueeze(1)           # (B, n_features) → (B, 1, n_features)
        x = self.stem(x)
        x = self.layer1(x)
        x = self.layer2(x)
        x = self.layer3(x)
        x = self.layer4(x)
        x = self.pool(x).squeeze(-1)  # (B, 512)
        return self.fc(x).squeeze(1)


# ── Generic sklearn wrapper for ALL DL classifiers ────────────────────────────

class _DLClassifier(BaseEstimator, ClassifierMixin):
    """
    Sklearn-compatible wrapper for any _MLP / _EEG_VGG16 / _EEG_ResNet model.
    Identical training loop for all; model architecture injected via `arch`.
    """

    def __init__(
        self,
        arch: str = "mlp",          # "mlp" | "vgg16" | "resnet"
        epochs: int = 50,
        batch_size: int = 64,
        lr: float = 1e-3,
        patience: int = 10,
        random_state: int = 42,
    ):
        self.arch = arch
        self.epochs = epochs
        self.batch_size = batch_size
        self.lr = lr
        self.patience = patience
        self.random_state = random_state

    # ── internal helpers ──────────────────────────────────────────────────────

    def _build_model(self, in_dim: int) -> nn.Module:
        """Instantiate the correct architecture based on self.arch."""
        if self.arch == "mlp":
            return _MLP(in_dim)
        elif self.arch == "vgg16":
            return _EEG_VGG16(in_dim)
        elif self.arch == "resnet":
            return _EEG_ResNet(in_dim)
        else:
            raise ValueError(f"Unknown arch: {self.arch!r}")

    def _make_val_split(
        self, X: np.ndarray, y: np.ndarray, groups: np.ndarray | None
    ) -> Tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
        """
        Reserve 20% of participants (or 20% of samples) for internal validation.
        Group-aware split prevents data leakage across participants.
        """
        if groups is not None:
            unique_g = np.unique(groups)
            n_val    = max(1, int(len(unique_g) * 0.2))
            rng      = np.random.default_rng(self.random_state)
            val_g    = set(rng.choice(unique_g, size=n_val, replace=False))
            val_mask = np.isin(groups, list(val_g))
            tr_mask  = ~val_mask
            if val_mask.sum() > 0 and tr_mask.sum() > 0:
                return X[tr_mask], y[tr_mask], X[val_mask], y[val_mask]
        # Fallback: random 80/20 split when group info is unavailable
        n   = len(X)
        idx = np.random.permutation(n)
        v   = max(1, int(n * 0.2))
        return X[idx[v:]], y[idx[v:]], X[idx[:v]], y[idx[:v]]

    # ── fit ───────────────────────────────────────────────────────────────────

    def fit(self, X: np.ndarray, y: np.ndarray, groups: np.ndarray | None = None):
        """Train the DL model with early stopping based on validation loss."""
        torch.manual_seed(self.random_state)
        np.random.seed(self.random_state)

        device  = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        X_arr   = np.array(X, dtype=np.float32)
        y_arr   = np.array(y, dtype=np.float32)
        g_arr   = np.array(groups) if groups is not None else None

        X_tr, y_tr, X_val, y_val = self._make_val_split(X_arr, y_arr, g_arr)

        # Class imbalance weight: upweight the minority (positive) class
        pos_w = torch.tensor(
            [(y_tr == 0).sum() / max((y_tr == 1).sum(), 1)], dtype=torch.float32
        ).to(device)

        loader = DataLoader(
            TensorDataset(torch.tensor(X_tr).to(device), torch.tensor(y_tr).to(device)),
            batch_size=self.batch_size, shuffle=True,
        )
        X_val_t = torch.tensor(X_val).to(device)
        y_val_t = torch.tensor(y_val).to(device)

        self.model_  = self._build_model(X_arr.shape[1]).to(device)
        optimizer    = torch.optim.Adam(self.model_.parameters(), lr=self.lr, weight_decay=1e-4)
        criterion    = nn.BCEWithLogitsLoss(pos_weight=pos_w)
        # Halve LR if validation loss plateaus for 5 epochs
        scheduler    = torch.optim.lr_scheduler.ReduceLROnPlateau(optimizer, patience=5, factor=0.5)

        best_val, patience_ct, best_state = float("inf"), 0, None

        for _ in range(self.epochs):
            self.model_.train()
            for xb, yb in loader:
                optimizer.zero_grad()
                loss = criterion(self.model_(xb), yb)
                loss.backward()
                nn.utils.clip_grad_norm_(self.model_.parameters(), 1.0)  # Prevent exploding gradients
                optimizer.step()

            self.model_.eval()
            with torch.no_grad():
                val_loss = criterion(self.model_(X_val_t), y_val_t).item()
            scheduler.step(val_loss)

            # Save the best weights and track patience for early stopping
            if val_loss < best_val - 1e-4:
                best_val, patience_ct = val_loss, 0
                best_state = {k: v.cpu().clone() for k, v in self.model_.state_dict().items()}
            else:
                patience_ct += 1
                if patience_ct >= self.patience:
                    break  # Early stopping triggered

        # Restore the best checkpoint before returning
        if best_state is not None:
            self.model_.load_state_dict(best_state)

        self.device_  = device
        self.classes_ = np.array([0, 1])
        return self

    # ── predict ───────────────────────────────────────────────────────────────

    def predict(self, X: np.ndarray) -> np.ndarray:
        """Run inference: logits → sigmoid → threshold at 0.5 → {0, 1}."""
        self.model_.eval()
        with torch.no_grad():
            logits = self.model_(
                torch.tensor(np.array(X, dtype=np.float32)).to(self.device_)
            )
            # clamp prevents overflow; clip guarantees strictly {0,1}
            preds = (torch.sigmoid(logits.clamp(-20, 20)) >= 0.5).long().cpu().numpy()
        return np.clip(preds, 0, 1).astype(int)


# ──────────────────────────────────────────────────────────────────────────────
# Model registry
# ──────────────────────────────────────────────────────────────────────────────

def build_models(feature_cols: List[str], cfg: Config) -> Dict[str, Pipeline]:
    """Return a fresh dict of {name: Pipeline} for every model in the study."""

    def fresh_pre() -> ColumnTransformer:
        return clone(make_preprocessor(feature_cols, cfg))  # Clone avoids state leakage across folds

    def fresh_pre_no_pca() -> ColumnTransformer:
        return clone(make_preprocessor_no_pca(feature_cols))  # DL models receive full-dimensional input

    dl_kwargs = dict(
        epochs=cfg.dl_epochs,
        batch_size=cfg.dl_batch_size,
        lr=cfg.dl_lr,
        patience=cfg.dl_patience,
        random_state=cfg.random_state,
    )

    return {
        # ── classical ML ──────────────────────────────────────────────────────
        "LR": Pipeline([
            ("pre", fresh_pre()),
            ("clf", LogisticRegression(max_iter=6000, class_weight="balanced",
                                       random_state=cfg.random_state)),
        ]),
        "LinearSVC": Pipeline([
            ("pre", fresh_pre()),
            ("clf", LinearSVC(class_weight="balanced", dual=False,
                              max_iter=12000, random_state=cfg.random_state)),
        ]),
        "SVM_RBF": Pipeline([
            ("pre", fresh_pre()),
            ("clf", SVC(kernel="rbf", class_weight="balanced",
                        probability=False, random_state=cfg.random_state)),
        ]),
        "HGB": Pipeline([
            ("pre", fresh_pre()),
            ("clf", HistGradientBoostingClassifier(class_weight="balanced",
                                                   random_state=cfg.random_state)),
        ]),
        "RF": Pipeline([
            ("pre", fresh_pre()),
            ("clf", RandomForestClassifier(n_estimators=300,
                                           class_weight="balanced_subsample",
                                           random_state=cfg.random_state,
                                           n_jobs=-1)),
        ]),
        "KNN": Pipeline([
            ("pre", fresh_pre()),
            ("clf", KNeighborsClassifier(weights="distance")),  # Closer neighbors get higher vote weight
        ]),
        "XGB": Pipeline([
            ("pre", fresh_pre()),
            ("clf", XGBClassifier(n_estimators=300, max_depth=6,
                                  learning_rate=0.1, subsample=0.8,
                                  colsample_bytree=0.8, eval_metric="logloss",
                                  random_state=cfg.random_state)),
        ]),

        # ── deep-learning (1-D EEG architectures) ────────────────────────────
        "MLP": Pipeline([
            ("pre", fresh_pre_no_pca()),
            ("clf", _DLClassifier(arch="mlp", **dl_kwargs)),
        ]),
        "EEG_VGG16": Pipeline([          # 1-D adaptation of VGG-16
            ("pre", fresh_pre_no_pca()),
            ("clf", _DLClassifier(arch="vgg16", **dl_kwargs)),
        ]),
        "EEG_ResNet": Pipeline([         # 1-D adaptation of ResNet-18
            ("pre", fresh_pre_no_pca()),
            ("clf", _DLClassifier(arch="resnet", **dl_kwargs)),
        ]),
    }


# ──────────────────────────────────────────────────────────────────────────────
# Metrics  — now returns classification_report dict as well
# ──────────────────────────────────────────────────────────────────────────────

def compute_binary_metrics(y_true: pd.Series, y_pred: np.ndarray) -> Dict[str, float]:
    """Compute the five core classification metrics for one fold."""
    return {
        "Accuracy":          float(accuracy_score(y_true, y_pred)),
        "Balanced_Accuracy": float(balanced_accuracy_score(y_true, y_pred)),
        "Precision":         float(precision_score(y_true, y_pred, zero_division=0)),
        "Recall":            float(recall_score(y_true, y_pred, zero_division=0)),
        "F1":                float(f1_score(y_true, y_pred, zero_division=0)),
    }


def flatten_confusion_matrix(y_true: pd.Series, y_pred: np.ndarray) -> Dict[str, int]:
    """Return TN, FP, FN, TP as a flat dict for easy CSV export."""
    tn, fp, fn, tp = confusion_matrix(y_true, y_pred, labels=[0, 1]).ravel()
    return {"TN": int(tn), "FP": int(fp), "FN": int(fn), "TP": int(tp)}


# ──────────────────────────────────────────────────────────────────────────────
# Cross-validation loop
# ──────────────────────────────────────────────────────────────────────────────

def _fit_dl(pipeline: Pipeline, X_train, y_train, groups_train):
    """Manually fit a DL pipeline so participant groups can be passed to the classifier."""
    pre = pipeline.named_steps["pre"]
    clf = pipeline.named_steps["clf"]
    X_tr_pre = pre.fit_transform(X_train, y_train)
    clf.fit(X_tr_pre, y_train.to_numpy(), groups=groups_train.to_numpy())


def _predict_dl(pipeline: Pipeline, X_test) -> np.ndarray:
    """Run the already-fitted preprocessor then the DL classifier."""
    pre = pipeline.named_steps["pre"]
    clf = pipeline.named_steps["clf"]
    return clf.predict(pre.transform(X_test))


DL_MODELS = {"MLP", "EEG_VGG16", "EEG_ResNet"}  # Models that need group-aware manual fitting


def run_grouped_cross_validation(
    X: pd.DataFrame,
    y: pd.Series,
    groups: pd.Series,
    feature_cols: List[str],
    cfg: Config,
) -> Tuple[pd.DataFrame, Dict[str, List[np.ndarray]]]:
    """
    Stratified Group K-Fold CV: each participant appears in only one fold.
    Returns per-fold metrics (detailed_df) and accumulated predictions (all_preds)
    for computing the overall classification report across all folds.
    """

    unique_groups = groups.nunique()
    n_splits = min(cfg.outer_splits, unique_groups)  # Can't have more folds than participants
    if n_splits < 2:
        raise ValueError(f"Need ≥2 participants for grouped CV, found {unique_groups}.")

    # StratifiedGroupKFold: preserves class ratio AND keeps participants together
    cv = StratifiedGroupKFold(n_splits=n_splits, shuffle=True, random_state=cfg.random_state)

    rows: List[Dict] = []
    all_preds: Dict[str, Tuple[List, List]] = {}   # model → (y_true_all, y_pred_all)

    for fold_idx, (train_idx, test_idx) in enumerate(cv.split(X, y, groups=groups), start=1):
        X_train, X_test = X.iloc[train_idx], X.iloc[test_idx]
        y_train, y_test = y.iloc[train_idx], y.iloc[test_idx]
        groups_train    = groups.iloc[train_idx]

        # Compute class imbalance ratio for XGBoost's scale_pos_weight
        n_neg = (y_train == 0).sum()
        n_pos = (y_train == 1).sum()
        scale_pos_weight = float(n_neg) / max(float(n_pos), 1.0)

        models = build_models(feature_cols, cfg)
        models["XGB"].set_params(clf__scale_pos_weight=scale_pos_weight)

        for model_name, pipeline in models.items():
            # DL models need group-aware fitting; sklearn models use the standard API
            if model_name in DL_MODELS:
                _fit_dl(pipeline, X_train, y_train, groups_train)
                y_pred = _predict_dl(pipeline, X_test)
            else:
                pipeline.fit(X_train, y_train)
                y_pred = pipeline.predict(X_test)

            # ── NEW: extract macro average for this fold ──────────────────────
            fold_report = classification_report(
                y_test, y_pred, digits=4, zero_division=0, output_dict=True
            )
            ma = fold_report.get("macro avg", {})
            # ─────────────────────────────────────────────────────────────────

            rows.append({
                "Algorithm":       model_name,
                "Fold":            fold_idx,
                "Train_Size":      int(len(train_idx)),
                "Test_Size":       int(len(test_idx)),
                **compute_binary_metrics(y_test, y_pred),
                **flatten_confusion_matrix(y_test, y_pred),
                "Macro_Precision": round(ma.get("precision", 0.0), 4),  # ← NEW
                "Macro_Recall":    round(ma.get("recall",    0.0), 4),  # ← NEW
                "Macro_F1":        round(ma.get("f1-score",  0.0), 4),  # ← NEW
            })

            # Accumulate predictions across folds for the global classification report
            if model_name not in all_preds:
                all_preds[model_name] = ([], [])
            all_preds[model_name][0].extend(y_test.tolist())
            all_preds[model_name][1].extend(y_pred.tolist())

    return pd.DataFrame(rows), all_preds


# ──────────────────────────────────────────────────────────────────────────────
# Summary & classification report
# ──────────────────────────────────────────────────────────────────────────────

def summarize_results(detailed: pd.DataFrame) -> pd.DataFrame:
    """Aggregate per-fold metrics into mean ± std for each algorithm."""
    metric_cols = ["Accuracy", "Balanced_Accuracy", "Precision", "Recall", "F1",
                   "Macro_Precision", "Macro_Recall", "Macro_F1"]  # ← NEW
    return (
        detailed.groupby("Algorithm")[metric_cols]
        .agg(["mean", "std"])
        .reset_index()
    )


def build_classification_report_text(
    all_preds: Dict[str, Tuple[List, List]],
    target_names: List[str] | None = None,
) -> str:
    """
    Produce a full sklearn classification_report for each model,
    computed over ALL folds combined (concatenated true/pred arrays).
    This gives the same format used in academic papers' results tables.
    """
    if target_names is None:
        target_names = ["Not Focused (0)", "Focused (1)"]

    lines = [
        "=" * 70,
        "CLASSIFICATION REPORT  —  All folds combined",
        "=" * 70,
    ]

    for model_name, (y_true_all, y_pred_all) in all_preds.items():
        y_true = np.array(y_true_all)
        y_pred = np.array(y_pred_all)

        report = classification_report(
            y_true, y_pred,
            target_names=target_names,
            digits=4,
            zero_division=0,
        )

        lines += [
            "",
            f"Model: {model_name}",
            "-" * 50,
            report,
        ]

    lines += ["=" * 70]
    return "\n".join(lines)


# ──────────────────────────────────────────────────────────────────────────────
# Entry point
# ──────────────────────────────────────────────────────────────────────────────

def main() -> None:
    # Resolve paths relative to the project root (two levels above this script)
    base_dir = Path(__file__).resolve().parent.parent
    cfg = Config(dataset_path=base_dir / "data" / "datasets" / "dataset_final.csv")

    detailed_path = base_dir / cfg.out_detailed
    summary_path  = base_dir / cfg.out_summary
    report_path   = base_dir / cfg.out_report

    for p in (detailed_path, summary_path, report_path):
        ensure_parent_dir(p)

    df = load_dataset(cfg.dataset_path)
    feature_cols = select_eeg_feature_columns(df, cfg)

    # Replace infinities with NaN so the imputer handles them uniformly
    X = df[feature_cols].apply(pd.to_numeric, errors="coerce").replace([np.inf, -np.inf], np.nan)
    y = df[cfg.label_col]
    groups = df[cfg.group_col].astype(str)

    print(f"Dataset shape    : {df.shape}")
    print(f"Participants     : {groups.nunique()}")
    print(f"Classes          : {sorted(y.unique().tolist())}")
    print(f"EEG features     : {len(feature_cols)}")
    print(f"First 10 features: {feature_cols[:10]}")

    # ── run CV ────────────────────────────────────────────────────────────────
    detailed, all_preds = run_grouped_cross_validation(X, y, groups, feature_cols, cfg)
    summary = summarize_results(detailed)

    # ── classification report ─────────────────────────────────────────────────
    report_text = build_classification_report_text(all_preds)

    # ── save all outputs ──────────────────────────────────────────────────────
    detailed.to_csv(detailed_path, index=False)
    summary.to_csv(summary_path, index=False)
    report_path.write_text(report_text, encoding="utf-8")

    print(f"\n[Saved] {detailed_path}")
    print(f"\n[Saved] {summary_path}")
    print(f"\n[Saved] {report_path}")

    print("\n=== Classification Summary (mean ± std across folds) ===")
    print(summary.to_string(index=False))

    print("\n=== Classification Report (all folds combined) ===")
    print(report_text)


if __name__ == "__main__":
    main()