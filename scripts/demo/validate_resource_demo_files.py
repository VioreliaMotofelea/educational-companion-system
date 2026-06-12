#!/usr/bin/env python3
"""Light validation for datasets/demo/resource-files/ and demo-resource-file-map.json."""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FILES_DIR = ROOT / "datasets" / "demo" / "resource-files"
MAP_PATH = FILES_DIR / "demo-resource-file-map.json"
SUPPORTED = {".txt", ".md", ".markdown", ".docx", ".pdf"}
GENERATED = {"web-development-accessibility-lab.docx", "ai-classification-metrics-worksheet.pdf"}


def main() -> int:
    errors: list[str] = []
    entries: list = []

    if not MAP_PATH.is_file():
        errors.append(f"Missing map: {MAP_PATH}")
    else:
        try:
            entries = json.loads(MAP_PATH.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            errors.append(f"Invalid JSON in map: {exc}")
            entries = []
        if not isinstance(entries, list):
            errors.append("Map root must be a JSON array")
            entries = []

        seen_files: set[str] = set()
        for i, entry in enumerate(entries):
            if not isinstance(entry, dict):
                errors.append(f"Entry {i} is not an object")
                continue
            name = entry.get("file")
            if not name:
                errors.append(f"Entry {i} missing file")
                continue
            if name in seen_files:
                errors.append(f"Duplicate map entry: {name}")
            seen_files.add(name)
            path = FILES_DIR / name
            if not path.is_file():
                errors.append(f"Mapped file missing: {name}")
            ext = path.suffix.lower() if path.exists() else Path(name).suffix.lower()
            if ext not in SUPPORTED:
                errors.append(f"Unsupported extension: {name}")

    for gen in GENERATED:
        if not (FILES_DIR / gen).is_file():
            errors.append(f"Generated file missing (run generate script): {gen}")

    if errors:
        for err in errors:
            print(f"ERROR: {err}", file=sys.stderr)
        return 1

    print(f"OK: {len(entries)} map entries, all files present, supported extensions only.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
