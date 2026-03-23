from __future__ import annotations

from typing import Iterable, List, Any


def to_item_ids(recommendations: Iterable[Any]) -> List[str]:
    """
    Normalize recommender outputs into item id strings.

    Supports:
    - Pydantic/objects with `learningResourceId`
    - dicts with key `learningResourceId`
    - any other value (falls back to `str(rec)`)
    """
    item_ids: List[str] = []
    for rec in recommendations:
        if hasattr(rec, "learningResourceId"):
            item_ids.append(str(rec.learningResourceId))
        elif isinstance(rec, dict) and "learningResourceId" in rec:
            item_ids.append(str(rec["learningResourceId"]))
        else:
            item_ids.append(str(rec))
    return item_ids

