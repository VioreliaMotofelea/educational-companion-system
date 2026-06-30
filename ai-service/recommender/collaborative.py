import hashlib
import threading

import numpy as np
import pandas as pd
from sklearn.metrics.pairwise import cosine_similarity

from models.recommendation_models import RecommendationItem

_collab_matrix_cache: tuple | None = None
_collab_vectors_cache: tuple | None = None
_collab_lock = threading.Lock()


def clear_collaborative_matrix_cache() -> None:
    global _collab_matrix_cache, _collab_vectors_cache
    with _collab_lock:
        _collab_matrix_cache = None
        _collab_vectors_cache = None


def _normalize_user_id(user_id) -> str:
    return str(user_id)


def _normalize_resource_id(resource_id) -> str:
    return str(resource_id)


def _extract_completed_pairs(all_users_interactions) -> set[tuple[str, str]]:
    pairs: set[tuple[str, str]] = set()
    for row in all_users_interactions:
        if row.get("interactionType") != "Completed":
            continue
        user_id = row.get("userId")
        resource_id = row.get("learningResourceId")
        if user_id is None or resource_id is None:
            continue
        pairs.add((_normalize_user_id(user_id), _normalize_resource_id(resource_id)))
    return pairs


def _collaborative_cache_fingerprint(all_users_interactions, resources) -> str:
    h = hashlib.sha256()
    for user_id, resource_id in sorted(_extract_completed_pairs(all_users_interactions)):
        h.update(user_id.encode("utf-8", errors="surrogatepass"))
        h.update(b"\0")
        h.update(resource_id.encode("utf-8", errors="surrogatepass"))
        h.update(b"\n")
    for resource in resources:
        h.update(_normalize_resource_id(resource["id"]).encode("utf-8", errors="surrogatepass"))
        h.update(b"\n")
    return h.hexdigest()


def prepare_collaborative_matrix(
    all_users_interactions,
    resources,
) -> tuple[pd.DataFrame, np.ndarray]:
    """
    Matrix values are binary implicit feedback: 1 if Completed, else 0.
    """
    global _collab_matrix_cache, _collab_vectors_cache

    pairs = _extract_completed_pairs(all_users_interactions)
    if not pairs:
        empty = pd.DataFrame()
        return empty, np.empty((0, 0), dtype=np.float64)

    resource_id_set = {_normalize_resource_id(r["id"]) for r in resources}
    cache_key = _collaborative_cache_fingerprint(all_users_interactions, resources)

    snapshot = _collab_matrix_cache
    if snapshot is not None:
        snap_key, snap_matrix = snapshot
        if snap_key == cache_key:
            vec_snap = _collab_vectors_cache
            if vec_snap is not None and vec_snap[0] == cache_key:
                return snap_matrix, vec_snap[1]

    with _collab_lock:
        snapshot = _collab_matrix_cache
        if snapshot is not None:
            snap_key, snap_matrix = snapshot
            if snap_key == cache_key:
                vec_snap = _collab_vectors_cache
                if vec_snap is not None and vec_snap[0] == cache_key:
                    return snap_matrix, vec_snap[1]

        rows = [
            {"userId": user_id, "resourceId": resource_id, "completed": 1}
            for user_id, resource_id in pairs
        ]
        df = pd.DataFrame(rows)
        matrix = df.pivot_table(
            index="userId",
            columns="resourceId",
            values="completed",
            aggfunc="max",
            fill_value=0,
        )
        union_cols = sorted(
            {_normalize_resource_id(c) for c in matrix.columns} | resource_id_set,
            key=str,
        )
        matrix = matrix.reindex(columns=union_cols, fill_value=0)
        matrix.index = matrix.index.map(_normalize_user_id)
        matrix.columns = matrix.columns.map(_normalize_resource_id)
        matrix = matrix.clip(upper=1.0)
        all_vecs = matrix.to_numpy(dtype=np.float64, copy=False)
        _collab_matrix_cache = (cache_key, matrix)
        _collab_vectors_cache = (cache_key, all_vecs)
        return matrix, all_vecs


def collaborative_score_map_from_prepared(
    user_id: str,
    matrix: pd.DataFrame,
    all_vecs: np.ndarray,
    *,
    top_k: int,
    neighbors_k: int = 5,
) -> dict[str, float]:
    user_id = _normalize_user_id(user_id)
    if matrix.empty or user_id not in matrix.index:
        return {}

    user_idx = matrix.index.get_loc(user_id)
    user_vec = all_vecs[user_idx : user_idx + 1]
    sim_row = cosine_similarity(user_vec, all_vecs)[0]
    similarity_series = pd.Series(sim_row, index=matrix.index)

    similar_users = (
        similarity_series.drop(labels=[user_id], errors="ignore")
        .loc[lambda s: s > 0]
        .sort_values(ascending=False)
        .head(neighbors_k)
    )
    if similar_users.empty:
        return {}

    neighbor_matrix = matrix.loc[similar_users.index]
    weighted_scores = np.dot(similar_users.values, neighbor_matrix.to_numpy(dtype=np.float64, copy=False))
    scores = pd.Series(weighted_scores, index=neighbor_matrix.columns)

    user_items = matrix.loc[user_id]
    candidate_scores = scores[user_items == 0]
    ranked = candidate_scores.sort_values(ascending=False)

    return {
        _normalize_resource_id(resource_id): float(score)
        for resource_id, score in ranked.head(top_k).items()
    }


def generate_collaborative(
    user_id,
    all_users_interactions,
    resources,
    top_k=20,
    neighbors_k=5,
    *,
    prepared: tuple[pd.DataFrame, np.ndarray] | None = None,
):

    user_id = _normalize_user_id(user_id)

    if prepared is not None:
        matrix, all_vecs = prepared
        score_map = collaborative_score_map_from_prepared(
            user_id,
            matrix,
            all_vecs,
            top_k=top_k,
            neighbors_k=neighbors_k,
        )
        return [
            RecommendationItem(
                learningResourceId=rid,
                score=round(score, 4),
                algorithmUsed="Collaborative-Cosine-KNN",
                explanation="Recommended because similar users completed this resource",
            )
            for rid, score in score_map.items()
        ]

    pairs = _extract_completed_pairs(all_users_interactions)
    if not pairs:
        return []

    matrix, all_vecs = prepare_collaborative_matrix(all_users_interactions, resources)
    score_map = collaborative_score_map_from_prepared(
        user_id,
        matrix,
        all_vecs,
        top_k=top_k,
        neighbors_k=neighbors_k,
    )
    return [
        RecommendationItem(
            learningResourceId=rid,
            score=round(score, 4),
            algorithmUsed="Collaborative-Cosine-KNN",
            explanation="Recommended because similar users completed this resource",
        )
        for rid, score in score_map.items()
    ]
