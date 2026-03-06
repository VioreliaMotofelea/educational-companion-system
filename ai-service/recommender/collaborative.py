import numpy as np
import pandas as pd
from sklearn.metrics.pairwise import cosine_similarity


def generate_collaborative(
    user_id,
    all_users_interactions,
    resources,
    top_k=20,
    neighbors_k=5
):
    """
    Collaborative filtering using user-based cosine similarity.

    Steps:
    1. Build user-resource interaction matrix
    2. Compute cosine similarity between users
    3. Select top-K nearest neighbors
    4. Aggregate neighbor interactions
    5. Recommend unseen resources
    """

    # -----------------------------
    # 1. Build interaction dataframe
    # -----------------------------
    interactions = [
        (i["userId"], i["learningResourceId"])
        for i in all_users_interactions
        if i["interactionType"] == "Completed"
    ]

    if not interactions:
        return []

    df = pd.DataFrame(interactions, columns=["userId", "resourceId"])

    # -----------------------------
    # 2. Create user-item matrix
    # -----------------------------
    matrix = pd.pivot_table(
        df,
        index="userId",
        columns="resourceId",
        aggfunc=len,
        fill_value=0
    )

    # ensure all resources exist as columns
    for r in resources:
        if r["id"] not in matrix.columns:
            matrix[r["id"]] = 0

    matrix = matrix.sort_index(axis=1)

    if user_id not in matrix.index:
        return []

    # -----------------------------
    # 3. Compute user similarity
    # -----------------------------
    similarity = cosine_similarity(matrix)

    similarity_df = pd.DataFrame(
        similarity,
        index=matrix.index,
        columns=matrix.index
    )

    # -----------------------------
    # 4. Find nearest neighbors
    # -----------------------------
    similar_users = (
        similarity_df[user_id]
        .drop(user_id)
        .sort_values(ascending=False)
        .head(neighbors_k)
    )

    if similar_users.empty:
        return []

    # -----------------------------
    # 5. Weighted recommendation scores
    # -----------------------------
    neighbor_matrix = matrix.loc[similar_users.index]

    weighted_scores = np.dot(
        similar_users.values,
        neighbor_matrix
    )

    scores = pd.Series(
        weighted_scores,
        index=neighbor_matrix.columns
    )

    # remove items already completed by user
    user_items = matrix.loc[user_id]

    candidate_scores = scores[user_items == 0]

    ranked = candidate_scores.sort_values(ascending=False)

    # -----------------------------
    # 6. Build recommendation list
    # -----------------------------
    recommendations = []

    for resource_id, score in ranked.head(top_k).items():

        recommendations.append({
            "learningResourceId": resource_id,
            "score": round(float(score), 4),
            "algorithmUsed": "Collaborative-Cosine-KNN",
            "explanation": "Recommended because similar users completed this resource"
        })

    return recommendations
