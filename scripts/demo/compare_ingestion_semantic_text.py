#!/usr/bin/env python3
"""Measure semantic text enrichment from extractedTextSummary (thesis evaluation)."""

from __future__ import annotations

import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
AI_SERVICE = REPO_ROOT / "ai-service"
sys.path.insert(0, str(AI_SERVICE))

from recommender.content_semantic import build_resource_semantic_text  # noqa: E402

RESOURCE_066 = {
    "id": "00000000-0000-4000-8000-000000000066",
    "title": "Week 3 Reading — Normalization",
    "topic": "Databases",
    "description": "Course reading on functional dependencies and normal forms (metadata only; no public link).",
    "contentType": "Article",
}

SUMMARY = (
    "Databases demo supplementary notes: relational schemas, functional dependencies, "
    "BCNF decomposition, normalization reduces update anomalies in course materials."
)

QUERY = "database normalization BCNF functional dependencies course reading"


def _try_cosine(a: str, b: str) -> float | None:
    try:
        import numpy as np
        from sentence_transformers import SentenceTransformer
    except ImportError:
        return None

    model = SentenceTransformer("sentence-transformers/all-MiniLM-L6-v2")
    emb = model.encode([a, b], normalize_embeddings=True)
    return float(np.dot(emb[0], emb[1]))


def main() -> int:
    before = build_resource_semantic_text(RESOURCE_066)
    after = build_resource_semantic_text({**RESOURCE_066, "extractedTextSummary": SUMMARY})

    print("=== Semantic text: before vs after ingestion summary ===\n")
    print("--- BEFORE (metadata only) ---")
    print(before)
    print(f"\n(characters: {len(before)})\n")
    print("--- AFTER (+ extractedTextSummary) ---")
    print(after)
    print(f"\n(characters: {len(after)})\n")

    delta = len(after) - len(before)
    has_summary_line = "Summary:" in after and "Summary:" not in before
    print(f"Character delta: +{delta}")
    print(f"New Summary line in semantic text: {has_summary_line}")

    sim_before = _try_cosine(before, QUERY)
    sim_after = _try_cosine(after, QUERY)
    if sim_before is not None and sim_after is not None:
        print(f"\nCosine similarity to query ({QUERY!r}):")
        print(f"  before: {sim_before:.4f}")
        print(f"  after:  {sim_after:.4f}")
        print(f"  delta:  {sim_after - sim_before:+.4f}")
    else:
        print("\n(install sentence-transformers in ai-service venv for cosine metrics)")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
