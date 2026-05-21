from __future__ import annotations

import subprocess
import sys
from pathlib import Path


def base_dir() -> Path:
    return Path(__file__).resolve().parent.parent


# Returns the directory containing all runtime step scripts
def scripts_dir() -> Path:
    return base_dir() / "player_scripts"


# Runs a single pipeline step as a subprocess; raises CalledProcessError on failure
def run_step(script_name: str) -> None:
    script_path = scripts_dir() / script_name
    print(f"[PIPELINE] Running {script_name}")

    result = subprocess.run(
        [sys.executable, str(script_path)],
        capture_output=True,
        text=True,
    )

    if result.returncode != 0:
        print(f"[PIPELINE] FAILED {script_name}")

        # Print stdout and stderr only if they contain output
        if result.stdout.strip():
            print("[STDOUT]")
            print(result.stdout.strip())

        if result.stderr.strip():
            print("[STDERR]")
            print(result.stderr.strip())

        raise subprocess.CalledProcessError(
            result.returncode,
            result.args,
            output=result.stdout,
            stderr=result.stderr,
        )

    print(f"[PIPELINE] OK {script_name}")


def main() -> None:
    print("[PIPELINE] Start")

    # Ordered steps: data ingestion → preprocessing → alignment → features → labeling → calibration
    steps = [
    "0_prepare_current_player.py",
    "1_runtime_preprocessing.py",
    "2_runtime_alignment.py",
    "3_runtime_feature_extraction.py",
    "4_runtime_labeling.py",
    "5_runtime_calibration.py",
]

    for step in steps:
        run_step(step)  # Any failure stops the pipeline immediately

    print("[PIPELINE] Done")


if __name__ == "__main__":
    main()