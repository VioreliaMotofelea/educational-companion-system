import threading

import numpy as np
import pandas as pd
from sklearn.metrics.pairwise import cosine_similarity

from models.recommendation_models import RecommendationItem

_collab_matrix_cache: tuple | None = None
_collab_lock = threading.Lock()


def clear_collaborative_matrix_cache() -> None:
    global _collab_matrix_cache
    with _collab_lock:
        _collab_matrix_cache = None


def generate_collaborative(
    user_id,
    all_users_interactions,
    resources,
    top_k=20,
    neighbors_k=5,
):

    global _collab_matrix_cache

    # 1. Build interaction dataframe
    interactions = [
        (i["userId"], i["learningResourceId"])
        for i in all_users_interactions
        if i["interactionType"] == "Completed"
    ]

    if not interactions:
        return []

    resource_ids = [r["id"] for r in resources]
    resource_id_set = set(resource_ids)
    cache_key = (id(all_users_interactions), id(resources), len(resource_ids))

    matrix = None
    snapshot = _collab_matrix_cache
    if snapshot is not None:
        snap_key, snap_matrix = snapshot
        if snap_key == cache_key:
            matrix = snap_matrix

    if matrix is None:
        with _collab_lock:
            snapshot = _collab_matrix_cache
            if snapshot is not None:
                snap_key, snap_matrix = snapshot
                if snap_key == cache_key:
                    matrix = snap_matrix
            if matrix is None:
                df = pd.DataFrame(interactions, columns=["userId", "resourceId"])
                matrix = pd.pivot_table(
                    df,
                    index="userId",
                    columns="resourceId",
                    aggfunc=len,
                    fill_value=0,
                )
                union_cols = sorted(
                    set(matrix.columns) | resource_id_set,
                    key=lambda c: str(c),
                )
                matrix = matrix.reindex(columns=union_cols, fill_value=0)
                _collab_matrix_cache = (cache_key, matrix)

    if user_id not in matrix.index:
        return []

    # 3. User–user similarity for this user only (avoids O(n_users²) full matrix)
    user_vec = matrix.loc[[user_id]].to_numpy(dtype=np.float64, copy=False)
    all_vecs = matrix.to_numpy(dtype=np.float64, copy=False)
    sim_row = cosine_similarity(user_vec, all_vecs)[0]
    similarity_series = pd.Series(sim_row, index=matrix.index)

    similar_users = (
        similarity_series.drop(labels=[user_id], errors="ignore")
        .sort_values(ascending=False)
        .head(neighbors_k)
    )

    if similar_users.empty:
        return []

    # 4. Weighted recommendation scores
    neighbor_matrix = matrix.loc[similar_users.index]

    weighted_scores = np.dot(similar_users.values, neighbor_matrix)

    scores = pd.Series(weighted_scores, index=neighbor_matrix.columns)

    # remove items already completed by user
    user_items = matrix.loc[user_id]

    candidate_scores = scores[user_items == 0]

    ranked = candidate_scores.sort_values(ascending=False)

    # 5. Build recommendation list
    recommendations = []

    for resource_id, score in ranked.head(top_k).items():
        recommendations.append(
            RecommendationItem(
                learningResourceId=str(resource_id),
                score=round(float(score), 4),
                algorithmUsed="Collaborative-Cosine-KNN",
                explanation="Recommended because similar users completed this resource",
            )
        )

    return recommendations
