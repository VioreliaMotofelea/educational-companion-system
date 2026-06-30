from __future__ import annotations

import hashlib
import threading
from typing import Any

import numpy as np
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity

from models.recommendation_models import RecommendationItem

_tfidf_cache: tuple[str, Any] | None = None
_tfidf_lock = threading.Lock()


def clear_content_based_tfidf_cache() -> None:
    global _tfidf_cache
    with _tfidf_lock:
        _tfidf_cache = None


def _tfidf_document_text(resource: dict) -> str:
    parts = [
        resource.get("title") or "",
        resource.get("topicName") or "",
        resource.get("topic") or "",
        resource.get("activityLabel") or "",
        resource.get("pedagogicalRole") or "",
        resource.get("contentFormat") or "",
        resource.get("difficultyLabel") or "",
        resource.get("semanticKeywords") or "",
        resource.get("weekInferred") or resource.get("week") or "",
        resource.get("coursePhase") or "",
        resource.get("learningContext") or "",
        resource.get("description") or "",
    ]
    return " ".join(str(p).strip() for p in parts if p)


def _resources_tfidf_fingerprint(resources: list) -> str:
    h = hashlib.sha256()
    h.update(b"tfidf_corpus_v3\n")
    for r in resources:
        h.update(str(r["id"]).encode("utf-8", errors="surrogatepass"))
        h.update(b"\0")
        h.update(_tfidf_document_text(r).encode("utf-8", errors="surrogatepass"))
        h.update(b"\n")
    return h.hexdigest()


def _get_or_build_tfidf_matrix(resources: list):
    global _tfidf_cache

    fp = _resources_tfidf_fingerprint(resources)
    snap = _tfidf_cache
    if snap is not None:
        snap_fp, snap_mat = snap
        if snap_fp == fp:
            return snap_mat

    with _tfidf_lock:
        snap = _tfidf_cache
        if snap is not None:
            snap_fp, snap_mat = snap
            if snap_fp == fp:
                return snap_mat
        corpus = [_tfidf_document_text(r) for r in resources]
        vectorizer = TfidfVectorizer(stop_words="english")
        tfidf_matrix = vectorizer.fit_transform(corpus)
        _tfidf_cache = (fp, tfidf_matrix)
        return tfidf_matrix


def prewarm_tfidf_matrix(resources: list) -> None:
    if resources:
        _get_or_build_tfidf_matrix(resources)


def _completed_resource_indices(interactions: list, resources: list) -> list[int]:
    resource_index = {str(r["id"]): idx for idx, r in enumerate(resources)}
    indices: list[int] = []
    seen: set[int] = set()
    for interaction in interactions:
        if interaction.get("interactionType") != "Completed":
            continue
        idx = resource_index.get(str(interaction.get("learningResourceId")))
        if idx is not None and idx not in seen:
            seen.add(idx)
            indices.append(idx)
    return indices


def compute_tfidf_score_map(interactions, resources) -> dict[str, float]:
    if not interactions or not resources:
        return {}

    completed_indices = _completed_resource_indices(interactions, resources)
    if not completed_indices:
        return {}

    tfidf_matrix = _get_or_build_tfidf_matrix(resources)
    sim_rows = cosine_similarity(tfidf_matrix[completed_indices], tfidf_matrix)
    similarity_scores = np.asarray(sim_rows.mean(axis=0)).ravel()

    return {
        str(resources[idx]["id"]): float(similarity_scores[idx])
        for idx in range(len(resources))
    }


def _content_explanation_topic(resource: dict) -> str:
    topic = (resource.get("topic") or "").strip()
    return topic if topic else "a related topic"


def generate_content_based(user, interactions, resources, top_k=20):
    if not interactions:
        return []

    resource_by_id = {str(r["id"]): r for r in resources}

    completed_ids = [
        str(i["learningResourceId"])
        for i in interactions
        if i["interactionType"] == "Completed"
    ]

    if not completed_ids:
        return []

    score_map = compute_tfidf_score_map(interactions, resources)
    if not score_map:
        return []

    ranked_ids = sorted(score_map.keys(), key=lambda rid: score_map[rid], reverse=True)
    completed_set = set(completed_ids)

    recommendations = []

    for rid in ranked_ids:
        if rid in completed_set:
            continue

        resource = resource_by_id.get(rid)
        if resource is None:
            continue

        topic_label = _content_explanation_topic(resource)
        recommendations.append(
            RecommendationItem(
                learningResourceId=rid,
                score=round(score_map[rid], 4),
                algorithmUsed="ContentBased-TFIDF",
                explanation=f"Similar to resources you completed in {topic_label}",
            )
        )

        if len(recommendations) >= top_k:
            break

    return recommendations
