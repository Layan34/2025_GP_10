"""
kappa_alignment.py
==================
Computes global Cohen's Kappa between SAM focus label and d-prime binary.
One row per block per participant. Output: per-participant report + global kappa.
"""

from __future__ import annotations
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.metrics import cohen_kappa_score

# ── Paths ─────────────────────────────────────────────────────────────────────
BASE_DIR = Path(__file__).resolve().parent.parent
DATASET  = BASE_DIR / "data" / "datasets" / "dataset_final.csv"
OUT_DIR  = BASE_DIR / "data" / "alignment"
OUT_DIR.mkdir(parents=True, exist_ok=True)


# Maps a kappa value to a qualitative label using Landis & Koch (1977) benchmarks
def interpret_kappa(k: float) -> str:
    if k > 0.80: return "Almost Perfect"
    elif k > 0.60: return "Substantial"
    elif k > 0.40: return "Moderate"
    elif k > 0.20: return "Fair"
    else: return "Poor"


def main() -> None:

    df = pd.read_csv(DATASET, low_memory=False)

    # Collapse sample-level rows to one row per participant-block, keeping first label and d'
    block_df = (
        df.groupby(["participant", "block"], sort=True)
        .agg(
            focus_label_binary=("focus_label_binary", "first"),
            beh_dprime=("beh_dprime", "first"),
        )
        .reset_index()
        .dropna(subset=["focus_label_binary", "beh_dprime"])
        .reset_index(drop=True)
    )

    # Binarize d' using the global median: blocks at or above median → 1 (high performance)
    median_dp = block_df["beh_dprime"].median()
    block_df["dprime_binary"] = (block_df["beh_dprime"] >= median_dp).astype(int)

    print("\n" + "=" * 65)
    print("  Per-Participant Values Entering the Kappa")
    print("  SAM focus label  vs  d-prime binary")
    print("=" * 65)
    print(f"  Global d-prime median used for binarization: {median_dp:.4f}")
    print(f"\n  {'Participant':<12} {'Block':>6} {'SAM':>6} {'DPrime_raw':>12} {'DPrime_binary':>14}")
    print("  " + "-" * 55)

    # Print each participant's block-level values for manual inspection
    for pid, group in block_df.groupby("participant"):
        for _, row in group.iterrows():
            print(
                f"  {str(pid):<12} "
                f"{int(row['block']):>6} "
                f"{int(row['focus_label_binary']):>6} "
                f"{row['beh_dprime']:>12.4f} "
                f"{int(row['dprime_binary']):>14}"
            )
        print("  " + "-" * 55)

    sam_vals = block_df["focus_label_binary"].tolist()
    dp_vals  = block_df["dprime_binary"].tolist()

    # Compute Cohen's Kappa: measures agreement between SAM labels and d'-based labels
    global_k = cohen_kappa_score(sam_vals, dp_vals)

    print(f"\n  Total blocks   : {len(block_df)}")
    print(f"  Participants   : {block_df['participant'].nunique()}")
    print(f"\n  Global Kappa   : {global_k:.4f}")
    print(f"  Interpretation : {interpret_kappa(global_k)}")
    print("=" * 65 + "\n")

    report = block_df[["participant", "block", "focus_label_binary", "beh_dprime", "dprime_binary"]].copy()
    report.columns = ["Participant", "Block", "SAM", "DPrime_raw", "DPrime_binary"]

    # Global kappa is stored only in the last row to avoid repeating it across all rows
    report["Global_Kappa"] = np.nan
    report.loc[report.index[-1], "Global_Kappa"] = round(global_k, 4)

    report.to_csv(OUT_DIR / "kappa_report.csv", index=False)
    print(f"  Saved: {OUT_DIR / 'kappa_report.csv'}")


if __name__ == "__main__":
    main()