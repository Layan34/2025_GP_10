from pathlib import Path

import matplotlib.pyplot as plt
import matplotlib.ticker as mticker
import numpy as np
import pandas as pd


# Display order for models in the chart (best to least preferred based on CV results)
MODEL_ORDER = ["RF", "HGB", "KNN", "SVM_RBF", "LR", "LinearSVC", "MLP", "XGB"]

# Human-readable multi-line labels for the x-axis ticks
MODEL_LABELS = {
    "RF": "Random\nForest",
    "HGB": "Hist Gradient\nBoosting",
    "KNN": "K-Nearest\nNeighbors",
    "SVM_RBF": "SVM\n(RBF)",
    "LR": "Logistic\nRegression",
    "LinearSVC": "Linear\nSVC",
    "MLP": "MLP\n(Neural Net)",
    "XGB": "XGBoost",
}

# Metric columns expected in the input CSV
METRICS = ["Accuracy", "Balanced_Accuracy", "F1", "Precision", "Recall"]

# Axis and legend labels for each metric
METRIC_LABELS = {
    "Accuracy": "Accuracy",
    "Balanced_Accuracy": "Balanced Acc.",
    "F1": "F1 Score",
    "Precision": "Precision",
    "Recall": "Recall",
}

# One distinct color per metric for visual separation in the grouped bar chart
METRIC_COLORS = {
    "Accuracy": "#43A047",          # green
    "F1": "#5E35B1",               # purple
    "Precision": "#1E88E5",        # blue
    "Recall": "#FB8C00",           # orange
    "Balanced_Accuracy": "#7CB342" # lighter green but different
}


# Loads the per-fold classification CSV and validates that all required columns exist
def load_results(path: Path) -> pd.DataFrame:
    if not path.exists():
        raise FileNotFoundError(f"File not found: {path}")

    df = pd.read_csv(path)

    required_cols = ["Algorithm"] + METRICS
    missing = [col for col in required_cols if col not in df.columns]

    if missing:
        raise ValueError(f"Missing required columns: {missing}")

    return df


# Averages per-fold metrics per model, reorders by MODEL_ORDER, and converts to percentage
def compute_summary(df: pd.DataFrame) -> pd.DataFrame:
    available_models = [m for m in MODEL_ORDER if m in df["Algorithm"].unique()]

    summary = (
        df.groupby("Algorithm")[METRICS]
        .mean()
        .reindex(available_models)
        .dropna()
        * 100  # Convert 0–1 metric values to percentage
    ).round(2)

    return summary


# Applies global matplotlib style settings for a clean, publication-ready look
def style_setup():
    plt.rcParams.update({
        "figure.facecolor": "white",
        "axes.facecolor": "white",
        "axes.edgecolor": "#444444",
        "axes.labelcolor": "black",
        "xtick.color": "black",
        "ytick.color": "black",
        "text.color": "black",
        "grid.color": "#DDDDDD",
        "grid.linewidth": 0.8,
        "font.family": "DejaVu Sans",
        "font.size": 10,
    })


# Draws grouped bars for all metrics per model; each metric group is offset symmetrically
def plot_multibar(ax, summary: pd.DataFrame):
    metrics = ["Accuracy", "F1", "Precision", "Recall", "Balanced_Accuracy"]

    n_models = len(summary)
    n_metrics = len(metrics)

    x = np.arange(n_models)
    width = 0.16  # Width of each individual bar

    # Spread bars evenly around each model's x position
    offsets = np.linspace(-(n_metrics - 1) / 2, (n_metrics - 1) / 2, n_metrics) * width

    for i, metric in enumerate(metrics):
        values = summary[metric].values
        color = METRIC_COLORS[metric]

        bars = ax.bar(
            x + offsets[i],
            values,
            width=width * 0.9,
            color=color,
            edgecolor="#444444",
            linewidth=0.6,
            label=METRIC_LABELS[metric],
            zorder=2,
        )

        # Annotate each bar with its numeric value above the bar top
        for bar, value in zip(bars, values):
            ax.text(
                bar.get_x() + bar.get_width() / 2,
                value + 0.7,
                f"{value:.2f}",
                ha="center",
                va="bottom",
                fontsize=6.5,
                fontweight="bold",
                color="#333333",
                zorder=3,
            )

    ax.set_xticks(x)
    ax.set_xticklabels(
        [MODEL_LABELS[m] for m in summary.index],
        fontsize=10,
        fontweight="bold",
    )

    ax.set_ylim(0, 100)

    ax.set_ylabel("Score (%)", fontsize=11, fontweight="bold")

    ax.set_title(
        "Multi-metric comparison (Accuracy / F1 / Precision / Recall / Balanced Acc.)",
        fontsize=13,
        fontweight="bold",
        pad=15,
    )

    ax.yaxis.set_major_locator(mticker.MultipleLocator(5))  # Y gridlines every 5%

    ax.grid(axis="y", linestyle="--", alpha=0.6)

    ax.tick_params(axis="x", length=0)  # Hide x-axis tick marks

    ax.spines["top"].set_visible(False)
    ax.spines["right"].set_visible(False)

    # Legend placed above the chart to avoid overlapping bars
    ax.legend(
        loc="upper center",
        bbox_to_anchor=(0.5, 1.12),
        ncol=n_metrics,
        fontsize=10,
        frameon=False,
    )


# Creates the figure, calls plot_multibar, and saves the output PNG at 300 DPI
def plot_all(summary: pd.DataFrame, output_path: Path) -> None:
    style_setup()

    fig, ax = plt.subplots(figsize=(16, 8), facecolor="white")
    ax.set_facecolor("white")

    plot_multibar(ax, summary)

    fig.tight_layout()

    output_path.parent.mkdir(parents=True, exist_ok=True)

    plt.savefig(
        output_path,
        dpi=300,
        bbox_inches="tight",
        facecolor="white",
    )

    plt.show()
    print(f"[Saved] {output_path}")


def main() -> None:
    base_dir = Path(__file__).resolve().parent.parent

    input_path = base_dir / "data" / "results" / "classification_detailed.csv"
    output_path = base_dir / "data" / "results" / "accuracy_comparison.png"

    df = load_results(input_path)
    summary = compute_summary(df)

    plot_all(summary, output_path)

    print("\n=== Summary ===")
    print(summary.to_string())


if __name__ == "__main__":
    main()