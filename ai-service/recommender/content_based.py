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


def _resources_tfidf_fingerprint(resources: list) -> str:
    h = hashlib.sha256()
    for r in resources:
        h.update(str(r["id"]).encode("utf-8", errors="surrogatepass"))
        h.update(b"\0")
        h.update((r.get("title") or "").encode("utf-8", errors="surrogatepass"))
        h.update(b"\0")
        h.update((r.get("topic") or "").encode("utf-8", errors="surrogatepass"))
        h.update(b"\0")
        h.update((r.get("description") or "").encode("utf-8", errors="surrogatepass"))
        h.update(b"\n")
    return h.hexdigest()


def generate_content_based(user, interactions, resources, top_k=20):
    global _tfidf_cache

    if not interactions:
        return []

    resource_by_id = {r["id"]: r for r in resources}

    completed_ids = [
        i["learningResourceId"]
        for i in interactions
        if i["interactionType"] == "Completed"
    ]

    if not completed_ids:
        return []

    fp = _resources_tfidf_fingerprint(resources)
    tfidf_matrix = None
    snap = _tfidf_cache
    if snap is not None:
        snap_fp, snap_mat = snap
        if snap_fp == fp:
            tfidf_matrix = snap_mat

    if tfidf_matrix is None:
        with _tfidf_lock:
            snap = _tfidf_cache
            if snap is not None:
                snap_fp, snap_mat = snap
                if snap_fp == fp:
                    tfidf_matrix = snap_mat
            if tfidf_matrix is None:
                corpus = [
                    f"{r.get('title', '')} {r.get('topic', '')} {r.get('description') or ''}"
                    for r in resources
                ]
                vectorizer = TfidfVectorizer(stop_words="english")
                tfidf_matrix = vectorizer.fit_transform(corpus)
                _tfidf_cache = (fp, tfidf_matrix)

    # Compute similarity between completed resources and all resources
    similarity_scores = np.zeros(len(resources))

    for completed_id in completed_ids:
        if completed_id in resource_by_id:
            idx = next(i for i, r in enumerate(resources) if r["id"] == completed_id)

            sim = cosine_similarity(tfidf_matrix[idx], tfidf_matrix)[0]

            similarity_scores += sim

    # Average similarity
    similarity_scores /= len(completed_ids)

    # Rank
    ranked_indices = np.argsort(similarity_scores)[::-1]

    recommendations = []

    for idx in ranked_indices:

        resource = resources[idx]

        # Skip already completed
        if resource["id"] in completed_ids:
            continue

        score = round(float(similarity_scores[idx]), 4)
        recommendations.append(
            RecommendationItem(
                learningResourceId=str(resource["id"]),
                score=score,
                algorithmUsed="ContentBased-TFIDF",
                explanation=f"Similar to resources you completed in {resource.get('topic', '')}",
            )
        )

        if len(recommendations) >= top_k:
            break

    return recommendations
