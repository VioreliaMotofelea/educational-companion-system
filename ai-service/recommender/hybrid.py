"""
Hybrid Recommendation System:

  TF-IDF Content-Based Filtering
  + Cosine KNN Collaborative Filtering
  + EDM Difficulty Adaptation
  = Hybrid Recommendation

  FinalScore = 0.5 * ContentScore + 0.3 * CollaborativeScore + 0.2 * DifficultyMatch
"""

from config import (
    HYBRID_CONTENT_WEIGHT,
    HYBRID_COLLAB_WEIGHT,
    HYBRID_DIFFICULTY_WEIGHT,
    DEFAULT_RECOMMENDATION_LIMIT,
)
from recommender.content_based import generate_content_based
from recommender.collaborative import generate_collaborative


def generate_hybrid(
    user,
    interactions,
    all_users_interactions,
    resources,
    mastery,
    top_k=None,
):
    """
    Combine content-based (TF-IDF), collaborative (KNN cosine), and EDM
    difficulty match into a single hybrid score per resource.

    - ContentScore: TF-IDF cosine similarity to user's completed resources.
    - CollaborativeScore: KNN user-based collaborative filtering.
    - DifficultyMatch: How well resource difficulty matches EDM suggested difficulty (0–1).

    Weights from config: 0.5 content, 0.3 collaborative, 0.2 difficulty.
    """
    top_k = top_k or DEFAULT_RECOMMENDATION_LIMIT
    user_id = user.get("userId")

    resource_by_id = {r["id"]: r for r in resources}
    completed_ids = {
        i["learningResourceId"]
        for i in interactions
        if i.get("interactionType") == "Completed"
    }

    # ---- 1. Content-based scores (TF-IDF) ----
    content_recs = generate_content_based(
        user, interactions, resources, top_k=len(resources)
    )
    content_map = {r["learningResourceId"]: r["score"] for r in content_recs}

    # ---- 2. Collaborative scores (KNN cosine) ----
    collab_recs = generate_collaborative(
        user_id,
        all_users_interactions,
        resources,
        top_k=len(resources),
    )
    collab_map = {r["learningResourceId"]: r["score"] for r in collab_recs}

    # ---- 3. EDM difficulty: suggested level 1–5 ----
    suggested_difficulty = 1
    if mastery:
        suggested_difficulty = mastery.get("suggestedDifficulty", 1)
    # Fallback to user preference if no mastery yet
    if suggested_difficulty is None and user.get("preferences"):
        pref = user["preferences"].get("preferredDifficulty")
        if pref is not None:
            suggested_difficulty = pref
    suggested_difficulty = max(1, min(5, int(suggested_difficulty)))

    # ---- 4. Normalize content and collab to [0, 1] ----
    max_content = max(content_map.values(), default=1) or 1
    max_collab = max(collab_map.values(), default=1) or 1

    # ---- 5. Candidates: all resources not completed by user ----
    candidates = [
        r for r in resources
        if r["id"] not in completed_ids
    ]

    scored = []

    for resource in candidates:
        rid = resource["id"]
        difficulty = resource.get("difficulty", 1)

        content_s = (content_map.get(rid, 0) / max_content)
        collab_s = (collab_map.get(rid, 0) / max_collab)

        # DifficultyMatch: 1 when resource difficulty == suggested; 0 when |diff| >= 4
        diff_gap = min(4, abs(difficulty - suggested_difficulty))
        difficulty_match = 1.0 - (diff_gap / 4.0)

        final_score = (
            HYBRID_CONTENT_WEIGHT * content_s
            + HYBRID_COLLAB_WEIGHT * collab_s
            + HYBRID_DIFFICULTY_WEIGHT * difficulty_match
        )

        scored.append({
            "resource": resource,
            "final_score": final_score,
            "content_s": content_s,
            "collab_s": collab_s,
            "difficulty_match": difficulty_match,
        })

    # Sort by final score descending, take top_k
    scored.sort(key=lambda x: x["final_score"], reverse=True)
    top = scored[:top_k]

    recommendations = []

    for item in top:
        r = item["resource"]
        recommendations.append({
            "learningResourceId": r["id"],
            "score": round(item["final_score"], 4),
            "algorithmUsed": "Hybrid",
            "explanation": (
                f"Content match {item['content_s']:.2f}, "
                f"similar users {item['collab_s']:.2f}, "
                f"difficulty fit {item['difficulty_match']:.2f} (suggested level {suggested_difficulty})."
            ),
        })

    return recommendations
