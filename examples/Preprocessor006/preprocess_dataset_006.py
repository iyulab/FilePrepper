#!/usr/bin/env python3
"""
Dataset 003 Preprocessing Example - Window Operations (Resample)
Purpose: Process press hydraulic motor current sensor data with 5-minute time-based aggregation

This example demonstrates:
- Time-series resampling with window operations
- Regular interval aggregation from irregular sensor data
- Mean aggregation for current readings
- Processing multiple related sensor files in batch
"""

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

# Configuration — set by the command line (see parse_args); the module-level names stay so the
# functions below read them as before.
FILEPREPPER_CLI = Path("fileprepper")
INPUT_DIR = Path("data")
OUTPUT_DIR = Path("processed-data")


def parse_args(argv=None):
    parser = argparse.ArgumentParser(description="5-minute window aggregation of press current data")
    parser.add_argument("--input-dir", default="data", help="folder holding the press current CSV files")
    parser.add_argument("--output-dir", default="processed-data", help="folder the aggregated files are written to")
    parser.add_argument("--cli", default="fileprepper",
                        help="FilePrepper CLI executable: a path, or a name found on PATH")
    return parser.parse_args(argv)

def run_fileprepper(command: list[str]) -> tuple[bool, str]:
    """Execute FilePrepper CLI command and return result"""
    try:
        result = subprocess.run(
            [str(FILEPREPPER_CLI)] + command,
            capture_output=True,
            text=True,
            check=True
        )
        return True, result.stdout
    except subprocess.CalledProcessError as e:
        return False, e.stderr

def preprocess_press_data():
    """Process all press current data files with 5-minute window aggregation"""

    # Create output directory
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    print("=" * 60)
    print(" Dataset 003: Press Current Data Processing")
    print(" 5-Minute Window Aggregation")
    print("=" * 60)
    print()

    # Process each press (1-4)
    for press_num in range(1, 5):
        input_file = INPUT_DIR / f"프레스 {press_num}호-유압모터 전류데이터.csv"
        output_file = OUTPUT_DIR / f"press{press_num}_5min.csv"

        print(f"Processing Press {press_num}...")
        print(f"  Input:  {input_file}")
        print(f"  Output: {output_file}")

        if not input_file.exists():
            print(f"  ⚠️  Input file not found, skipping...")
            print()
            continue

        # Apply 5-minute window resample aggregation
        success, output = run_fileprepper([
            "window",
            "-i", str(input_file),
            "-o", str(output_file),
            "--type", "resample",
            "--method", "mean",
            "--columns", "RMS[A]",
            "--time-column", "Time_s[s]",
            "--window", "5T",
            "--header"
        ])

        if success:
            print("  ✓ Successfully processed")

            # Calculate row count reduction
            input_rows = sum(1 for _ in open(input_file, 'r', encoding='utf-8'))
            output_rows = sum(1 for _ in open(output_file, 'r', encoding='utf-8'))
            reduction = ((input_rows - output_rows) * 100) / input_rows

            print(f"  📊 Rows: {input_rows:,} → {output_rows:,} ({reduction:.1f}% reduction)")
        else:
            print("  ✗ Processing failed")
            print(f"  Error: {output}")

        print()

    print("=" * 60)
    print(f" Preprocessing Complete")
    print(f" Output directory: {OUTPUT_DIR}")
    print("=" * 60)

if __name__ == "__main__":
    args = parse_args()
    INPUT_DIR = Path(args.input_dir)
    OUTPUT_DIR = Path(args.output_dir)
    resolved_cli = shutil.which(args.cli)
    if resolved_cli is None:
        print(f"Error: FilePrepper CLI not found: {args.cli}")
        print("Install it (dotnet tool install --global fileprepper-cli) or pass --cli <path>.")
        sys.exit(1)
    FILEPREPPER_CLI = Path(resolved_cli)

    if not INPUT_DIR.exists():
        print(f"Error: Input directory not found at {INPUT_DIR}")
        sys.exit(1)

    preprocess_press_data()
