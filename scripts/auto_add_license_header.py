#!/usr/bin/env python3
"""
Add Apache 2.0 license header to all .cs files in a C# project.
Skips files/directories matched by .gitignore.
"""

import sys
from pathlib import Path

# Get project root relative to this script (scripts/auto_add_license_header.py -> project_root)
PROJECT_ROOT = Path(__file__).resolve().parent.parent

try:
    import pathspec
except ImportError:
    print("Missing dependency: pathspec")
    print("Run: pip install pathspec")
    sys.exit(1)

# ── License header ────────────────────────────────────────────────────────────

HEADER = """\
// Copyright 2026 Cyanflower
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

"""

# Fallback: always skip these even without .gitignore
DEFAULT_SKIP_DIRS = {"bin", "obj", ".git", ".vs", "node_modules", "packages"}

# ── Helpers ───────────────────────────────────────────────────────────────────

def load_gitignore(root: Path):
    gitignore = root / ".gitignore"
    if not gitignore.exists():
        return None
    patterns = gitignore.read_text(encoding="utf-8").splitlines()
    return pathspec.PathSpec.from_lines("gitwildmatch", patterns)


def is_ignored(path: Path, root: Path, spec) -> bool:
    rel = path.relative_to(root).as_posix()
    # Always skip default dirs
    if any(part in DEFAULT_SKIP_DIRS for part in path.parts):
        return True
    # Check .gitignore
    if spec and spec.match_file(rel):
        return True
    return False


def already_has_header(content: str) -> bool:
    return content.lstrip().startswith("// Copyright")


def process_file(path: Path, dry_run: bool) -> str:
    """Returns: 'added', 'skipped', 'error'"""
    try:
        content = path.read_text(encoding="utf-8")
    except Exception as e:
        print(f"  [ERROR] Cannot read {path}: {e}")
        return "error"

    if already_has_header(content):
        return "skipped"

    if not dry_run:
        path.write_text(HEADER + content, encoding="utf-8")
    return "added"


# ── Main ──────────────────────────────────────────────────────────────────────

def main():
    import argparse

    parser = argparse.ArgumentParser(
        description="Add Apache 2.0 license header to .cs files."
    )
    parser.add_argument(
        "root",
        nargs="?",
        default=str(PROJECT_ROOT),
        help=f"Project root directory (default: {PROJECT_ROOT})",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Preview which files would be modified without writing",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    if not root.is_dir():
        print(f"Error: '{root}' is not a directory.")
        sys.exit(1)

    dry_run: bool = args.dry_run
    spec = load_gitignore(root)

    if spec:
        print(f"Loaded .gitignore from {root / '.gitignore'}")
    else:
        print("No .gitignore found, using default skip list only.")

    if dry_run:
        print("── DRY RUN: no files will be modified ──\n")

    counts = {"added": 0, "skipped": 0, "error": 0}

    for cs_file in sorted(root.rglob("*.cs")):
        if is_ignored(cs_file, root, spec):
            continue

        result = process_file(cs_file, dry_run)
        counts[result] += 1

        rel = cs_file.relative_to(root)
        if result == "added":
            prefix = "[DRY RUN] Would add" if dry_run else "Added"
            print(f"  + {prefix}: {rel}")
        elif result == "error":
            print(f"  x Error:  {rel}")
        # skipped files are silent; uncomment below to see them:
        # elif result == "skipped":
        #     print(f"  - Already has header: {rel}")

    print(
        f"\nDone. "
        f"{'Would add' if dry_run else 'Added'}: {counts['added']}  |  "
        f"Already had header: {counts['skipped']}  |  "
        f"Errors: {counts['error']}"
    )


if __name__ == "__main__":
    main()