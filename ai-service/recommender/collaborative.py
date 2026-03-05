import numpy as np
from sklearn.metrics.pairwise import cosine_similarity


def generate_collaborative(user_id, all_users_interactions, resources, top_k=20):

    # Build user-resource matrix
    user_ids = list({i["userId"] for i in all_users_interactions})
    resource_ids = [r["id"] for r in resources]

    user_index = {u: idx for idx, u in enumerate(user_ids)}
    resource_index = {r: idx for idx, r in enumerate(resource_ids)}

    matrix = np.zeros((len(user_ids), len(resource_ids)))

    for interaction in all_users_interactions:

        if interaction["interactionType"] == "Completed":

            u_idx = user_index[interaction["userId"]]
            r_idx = resource_index[interaction["learningResourceId"]]

            matrix[u_idx][r_idx] = 1

    # Compute similarity between users
    similarity_matrix = cosine_similarity(matrix)

    if user_id not in user_index:
        return []

    target_idx = user_index[user_id]

    similar_users = similarity_matrix[target_idx]

    recommendations_scores = np.dot(similar_users, matrix)

    ranked_indices = np.argsort(recommendations_scores)[::-1]

    recommendations = []

    for idx in ranked_indices:

        if matrix[target_idx][idx] == 1:
            continue  # skip already completed

        score = float(recommendations_scores[idx])

        recommendations.append({
            "learningResourceId": resource_ids[idx],
            "score": round(score, 4),
            "algorithmUsed": "Collaborative-Cosine",
            "explanation": "Popular among similar users"
        })

        if len(recommendations) >= top_k:
            break

    return recommendations