import numpy as np
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity

from models.recommendation_models import RecommendationItem


def generate_content_based(user, interactions, resources, top_k=20):

    if not interactions:
        return []

    # Build resource lookup
    resource_by_id = {r["id"]: r for r in resources}

    # Build corpus (Title + Topic + Description); handle None description
    corpus = [
        f"{r.get('title', '')} {r.get('topic', '')} {r.get('description') or ''}"
        for r in resources
    ]

    vectorizer = TfidfVectorizer(stop_words="english")
    tfidf_matrix = vectorizer.fit_transform(corpus)

    # Get resources user completed
    completed_ids = [
        i["learningResourceId"]
        for i in interactions
        if i["interactionType"] == "Completed"
    ]

    if not completed_ids:
        return []

    # Compute similarity between completed resources and all resources
    similarity_scores = np.zeros(len(resources))

    for completed_id in completed_ids:
        if completed_id in resource_by_id:
            idx = next(
                i for i, r in enumerate(resources)
                if r["id"] == completed_id
            )

            sim = cosine_similarity(
                tfidf_matrix[idx],
                tfidf_matrix
            )[0]

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
