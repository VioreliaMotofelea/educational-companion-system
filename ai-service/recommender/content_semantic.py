"""Semantic content-based recommendations using sentence-transformer embeddings."""

from __future__ import annotations

import hashlib
import logging
import os
import re
import shutil
import tempfile
import threading
from pathlib import Path
from typing import Any

import numpy as np

from config import SEMANTIC_CACHE_DIR, SEMANTIC_MODEL_NAME
from models.recommendation_models import RecommendationItem

logger = logging.getLogger(__name__)

_model_lock = threading.Lock()
_model_instances: dict[str, Any] = {}

_memory_cache: dict[tuple[str, str], np.ndarray] = {}
_memory_cache_lock = threading.Lock()

_MODEL_SLUG_RE = re.compile(r"[^a-zA-Z0-9._-]+")

CacheKey = tuple[str, str]  # (model_name, fingerprint)


def clear_content_semantic_cache() -> None:
    global _memory_cache
    with _memory_cache_lock:
        _memory_cache = {}


def clear_content_semantic_disk_cache(model_name: str | None = None) -> None:
    root = Path(SEMANTIC_CACHE_DIR)
    if model_name is None:
        if root.exists():
            shutil.rmtree(root, ignore_errors=True)
        return

    target = root / _model_slug(model_name)
    if target.exists():
        shutil.rmtree(target, ignore_errors=True)


def _dedupe_preserve_order(ids: list[str]) -> list[str]:
    seen: set[str] = set()
    unique: list[str] = []
    for rid in ids:
        if rid not in seen:
            seen.add(rid)
            unique.append(rid)
    return unique


def _resource_id_str(resource_id: Any) -> str:
    return str(resource_id)


_SEMANTIC_TEXT_VERSION = "v3"

# (label, resource dict keys to try in order)
_LABELED_FIELD_SPECS: tuple[tuple[str, tuple[str, ...]], ...] = (
    ("Title", ("title",)),
    ("Topic", ("topic",)),
    ("Description", ("description", "desc")),
    ("Summary", ("extractedTextSummary", "extracted_text_summary")),
    ("Content", ("content", "body", "text")),
    ("Type", ("contentType", "content_type")),
    ("Week", ("week", "weekInferred", "weekLabel", "week_label")),
    ("Phase", ("coursePhase", "course_phase")),
    ("Activity", ("activityLabel", "activityType", "activity_type", "activity", "resourceType", "resource_type")),
    ("Presentation", ("presentation", "code_presentation")),
)


def _normalize_semantic_field_value(value: Any) -> str:
    if value is None:
        return ""
    return " ".join(str(value).split()).strip()


def _first_present_semantic_field(resource: dict, keys: tuple[str, ...]) -> str:
    for key in keys:
        if key not in resource:
            continue
        normalized = _normalize_semantic_field_value(resource.get(key))
        if normalized:
            return normalized
    return ""


def _collect_semantic_labeled_fields(resource: dict) -> list[tuple[str, str]]:
    seen_values: set[str] = set()
    fields: list[tuple[str, str]] = []
    for label, keys in _LABELED_FIELD_SPECS:
        value = _first_present_semantic_field(resource, keys)
        if not value:
            continue
        dedupe_key = value.casefold()
        if dedupe_key in seen_values:
            continue
        seen_values.add(dedupe_key)
        fields.append((label, value))
    return fields


def _resources_semantic_fingerprint(resources: list) -> str:
    h = hashlib.sha256()
    h.update(_SEMANTIC_TEXT_VERSION.encode("ascii"))
    h.update(b"\n")
    for r in resources:
        h.update(_resource_id_str(r["id"]).encode("utf-8", errors="surrogatepass"))
        h.update(b"\0")
        h.update(build_resource_semantic_text(r).encode("utf-8", errors="surrogatepass"))
        h.update(b"\n")
    return h.hexdigest()


def build_resource_semantic_text(resource: dict) -> str:
    fields = _collect_semantic_labeled_fields(resource)
    if not fields:
        return "untitled resource"
    return "\n".join(f"{label}: {value}" for label, value in fields)


def _semantic_explanation_topic(resource: dict) -> str:
    topic = (resource.get("topic") or "").strip()
    return topic if topic else "a related topic"


def _model_slug(model_name: str) -> str:
    return _MODEL_SLUG_RE.sub("_", model_name).strip("_") or "model"


def _disk_cache_path(model_name: str, fingerprint: str) -> Path:
    cache_dir = SEMANTIC_CACHE_DIR / _model_slug(model_name)
    cache_dir.mkdir(parents=True, exist_ok=True)
    return cache_dir / f"{fingerprint}.npz"


def _load_disk_cache(model_name: str, fingerprint: str) -> np.ndarray | None:
    path = _disk_cache_path(model_name, fingerprint)
    if not path.is_file():
        return None
    try:
        data = np.load(path, allow_pickle=False)
        if str(data["fingerprint"]) != fingerprint:
            logger.warning("Semantic disk cache fingerprint mismatch; rebuilding.")
            return None
        if str(data["model_name"]) != model_name:
            logger.warning("Semantic disk cache model mismatch; rebuilding.")
            return None
        return np.asarray(data["embeddings"], dtype=np.float32)
    except (OSError, ValueError, KeyError) as exc:
        logger.warning("Failed to load semantic disk cache (%s); rebuilding.", exc)
        return None


def _save_disk_cache(model_name: str, fingerprint: str, embeddings: np.ndarray) -> None:
    path = _disk_cache_path(model_name, fingerprint)
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp_name = tempfile.mkstemp(suffix=".npz", dir=path.parent)
    tmp_path = Path(tmp_name)
    try:
        os.close(fd)
        np.savez_compressed(
            tmp_path,
            embeddings=embeddings.astype(np.float32, copy=False),
            fingerprint=np.array(fingerprint),
            model_name=np.array(model_name),
        )
        tmp_path.replace(path)
    except Exception:
        tmp_path.unlink(missing_ok=True)
        raise


def _get_sentence_transformer(model_name: str) -> Any:
    cached = _model_instances.get(model_name)
    if cached is not None:
        return cached
    with _model_lock:
        cached = _model_instances.get(model_name)
        if cached is not None:
            return cached
        from sentence_transformers import SentenceTransformer

        logger.info("Loading semantic model: %s", model_name)
        model = SentenceTransformer(model_name)
        _model_instances[model_name] = model
        return model


def _normalize_rows(matrix: np.ndarray) -> np.ndarray:
    if matrix.size == 0:
        return matrix
    norms = np.linalg.norm(matrix, axis=1, keepdims=True)
    norms = np.where(norms == 0, 1.0, norms)
    return matrix / norms


def _get_resource_embeddings(resources: list, model_name: str) -> np.ndarray:
    if not resources:
        return np.empty((0, 0), dtype=np.float32)

    fingerprint = _resources_semantic_fingerprint(resources)
    cache_key: CacheKey = (model_name, fingerprint)

    cached = _memory_cache.get(cache_key)
    if cached is not None:
        return cached

    with _memory_cache_lock:
        cached = _memory_cache.get(cache_key)
        if cached is not None:
            return cached

        embeddings = _load_disk_cache(model_name, fingerprint)
        if embeddings is None or embeddings.shape[0] != len(resources):
            texts = [build_resource_semantic_text(r) for r in resources]
            model = _get_sentence_transformer(model_name)
            embeddings = model.encode(
                texts,
                batch_size=64,
                show_progress_bar=False,
                convert_to_numpy=True,
                normalize_embeddings=True,
            )
            embeddings = np.asarray(embeddings, dtype=np.float32)
            _save_disk_cache(model_name, fingerprint, embeddings)
        else:
            embeddings = _normalize_rows(embeddings.astype(np.float32, copy=False))

        _memory_cache[cache_key] = embeddings
        return embeddings


def compute_semantic_score_map(
    interactions,
    resources,
    *,
    model_name: str | None = None,
) -> dict[str, float]:
    if not interactions or not resources:
        return {}

    resource_index = {_resource_id_str(r["id"]): idx for idx, r in enumerate(resources)}
    completed_ids = _dedupe_preserve_order(
        [
            _resource_id_str(i["learningResourceId"])
            for i in interactions
            if i.get("interactionType") == "Completed"
        ]
    )
    if not completed_ids:
        return {}

    model_name = model_name or SEMANTIC_MODEL_NAME
    embeddings = _get_resource_embeddings(resources, model_name)

    completed_indices = [
        resource_index[completed_id]
        for completed_id in completed_ids
        if completed_id in resource_index
    ]
    if not completed_indices:
        return {}

    user_vector = embeddings[completed_indices].mean(axis=0)
    user_norm = np.linalg.norm(user_vector)
    if user_norm > 0:
        user_vector = user_vector / user_norm

    similarity_scores = np.maximum(embeddings @ user_vector, 0.0)
    return {
        _resource_id_str(resources[idx]["id"]): float(similarity_scores[idx])
        for idx in range(len(resources))
    }


def generate_content_semantic(
    user,
    interactions,
    resources,
    top_k: int = 20,
    *,
    model_name: str | None = None,
) -> list[RecommendationItem]:
    _ = user  # same signature as TF-IDF path; user text is not embedded

    if not interactions or not resources:
        return []

    score_map = compute_semantic_score_map(
        interactions, resources, model_name=model_name or SEMANTIC_MODEL_NAME
    )
    if not score_map:
        return []

    completed_ids = _dedupe_preserve_order(
        [
            _resource_id_str(i["learningResourceId"])
            for i in interactions
            if i.get("interactionType") == "Completed"
        ]
    )
    completed_set = set(completed_ids)
    resource_by_id = {_resource_id_str(r["id"]): r for r in resources}
    ranked_ids = sorted(score_map.keys(), key=lambda rid: score_map[rid], reverse=True)

    recommendations: list[RecommendationItem] = []
    for rid in ranked_ids:
        if rid in completed_set:
            continue
        resource = resource_by_id.get(rid)
        if resource is None:
            continue
        topic_label = _semantic_explanation_topic(resource)
        recommendations.append(
            RecommendationItem(
                learningResourceId=rid,
                score=round(score_map[rid], 4),
                algorithmUsed="ContentBased-Semantic",
                explanation=(
                    f"Semantically similar to resources you completed in {topic_label}"
                ),
            )
        )
        if len(recommendations) >= top_k:
            break

    return recommendations
