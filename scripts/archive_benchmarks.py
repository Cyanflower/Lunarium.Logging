import os
import re
import shutil
from datetime import datetime
from pathlib import Path

def archive_benchmarks():
    # Get project root relative to this script (scripts/archive_benchmarks.py -> project_root)
    base_dir = Path(__file__).resolve().parent.parent
    history_dir = base_dir / "BenchmarkReports" / "History"
    results_dir = base_dir / "BenchmarkDotNet.Artifacts" / "results"

    if not results_dir.exists():
        print(f"❌ Error: Cannot find BenchmarkDotNet results directory: {results_dir}")
        return

    # 1. Scan History directory to determine the next Run number
    max_run = 0
    # Match folders like Dev-YYYY-MM-DD-Run.XXX
    pattern = re.compile(r"Dev-\d{4}-\d{2}-\d{2}-Run\.(\d{3})")

    if history_dir.exists():
        for item in history_dir.iterdir():
            if item.is_dir():
                match = pattern.match(item.name)
                if match:
                    run_num = int(match.group(1))
                    if run_num > max_run:
                        max_run = run_num

    # 2. Generate new folder path
    next_run_num = max_run + 1
    today_str = datetime.now().strftime("%Y-%m-%d")
    new_dev_folder_name = f"Dev-{today_str}-Run.{next_run_num:03d}"
    
    new_dev_dir = history_dir / new_dev_folder_name
    csv_dir = new_dev_dir / "csv"

    # 3. Create directories
    new_dev_dir.mkdir(parents=True, exist_ok=True)
    csv_dir.mkdir(parents=True, exist_ok=True)
    print(f"📁 Created new archive folder: {new_dev_dir}")
    print(f"📁 Created CSV sub-folder: {csv_dir}")

    # 4. Copy files
    md_count = 0
    csv_count = 0

    # Copy all .md files to the root of the new Dev folder
    for md_file in results_dir.glob("*.md"):
        shutil.copy2(md_file, new_dev_dir)
        md_count += 1

    # Copy all .csv files to the csv sub-folder
    for csv_file in results_dir.glob("*.csv"):
        shutil.copy2(csv_file, csv_dir)
        csv_count += 1

    print(f"✅ Archiving completed!")
    print(f"  - Copied {md_count} Markdown files to {new_dev_folder_name}/")
    print(f"  - Copied {csv_count} CSV files to {new_dev_folder_name}/csv/")

if __name__ == "__main__":
    archive_benchmarks()