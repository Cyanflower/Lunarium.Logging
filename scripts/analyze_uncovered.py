import xml.etree.ElementTree as ET
from pathlib import Path

# Get project root relative to this script (scripts/analyze_uncovered.py -> project_root)
PROJECT_ROOT = Path(__file__).resolve().parent.parent
results_path = PROJECT_ROOT / "TestResults"
output_path = PROJECT_ROOT / "CoverageReport" / "UncoveredLines.txt"

# Ensure output directory exists
output_path.parent.mkdir(parents=True, exist_ok=True)

if not results_path.exists():
    print(f"Directory not found: {results_path}")
    exit(0)

with open(output_path, "w", encoding="utf-8") as out_file:
    # Use rglob for recursive search of cobertura files
    for cobertura_file in results_path.rglob("coverage.cobertura.xml"):
        try:
            tree = ET.parse(cobertura_file)
            root = tree.getroot()
            for cls in root.findall(".//class"):
                lines = cls.find("lines")
                if lines is None: continue
                uncovered = [l.get("number") for l in lines.findall("line") if l.get("hits") == "0"]
                if uncovered:
                    out_file.write(f"[{cls.get('name')}] UncoveredLine: {', '.join(uncovered)}\n")
        except Exception as e:
            print(f"Error parsing {cobertura_file}: {e}")