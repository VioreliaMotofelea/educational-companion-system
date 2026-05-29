from collections import Counter

from config import (
    HYBRID_CONTENT_WEIGHT,
    HYBRID_COLLAB_WEIGHT,
    HYBRID_DIFFICULTY_WEIGHT,
    HYBRID_NO_DIFF_CONTENT_WEIGHT,
    HYBRID_NO_DIFF_COLLAB_WEIGHT,
    HYBRID_TOPIC_PREFERENCE_BONUS,
    HYBRID_NOVELTY_BONUS,
    HYBRID_TOPIC_REPEAT_PENALTY,
    DEFAULT_RECOMMENDATION_LIMIT,
)
from models.recommendation_models import RecommendationItem
from recommender.content_based import generate_content_based
from recommender.collaborative import generate_collaborative


HYBRID_VARIANT_FULL = "full"
HYBRID_VARIANT_NO_DIFFICULTY = "no_difficulty"
_HYBRID_VARIANTS = frozenset({HYBRID_VARIANT_FULL, HYBRID_VARIANT_NO_DIFFICULTY})

_ALGORITHM_BY_VARIANT = {
    HYBRID_VARIANT_FULL: "Hybrid-full",
    HYBRID_VARIANT_NO_DIFFICULTY: "Hybrid-no_difficulty",
}


def _parse_preferred_topics(user: dict) -> set[str]:
    prefs = user.get("preferences") or {}
    csv = prefs.get("preferredTopicsCsv")
    if not csv:
        return set()
    return {part.strip().lower() for part in str(csv).split(",") if part.strip()}


def _build_resource_popularity(all_users_interactions: list) -> dict[str, int]:
    # Lightweight novelty proxy: how often each resource appears in historical interactions.
    popularity: Counter[str] = Counter()
    for row in all_users_interactions:
        rid = row.get("learningResourceId")
        if rid:
            popularity[str(rid)] += 1
    return dict(popularity)


def _rerank_for_diversity(
    scored: list[dict],
    top_k: int,
    preferred_topics: set[str],
) -> list[dict]:
    selected: list[dict] = []
    pool = scored.copy()
    topic_counts: Counter[str] = Counter()

    while pool and len(selected) < top_k:
        best_idx = 0
        best_value = float("-inf")

        for idx, item in enumerate(pool):
            topic_key = str(item["resource"].get("topic", "")).strip().lower()
            pref_bonus = HYBRID_TOPIC_PREFERENCE_BONUS if topic_key in preferred_topics else 0.0
            repeat_penalty = HYBRID_TOPIC_REPEAT_PENALTY * topic_counts.get(topic_key, 0)
            value = item["final_score"] + pref_bonus + HYBRID_NOVELTY_BONUS * item["novelty_s"] - repeat_penalty
            if value > best_value:
                best_value = value
                best_idx = idx

        chosen = pool.pop(best_idx)
        selected.append(chosen)
        chosen_topic = str(chosen["resource"].get("topic", "")).strip().lower()
        topic_counts[chosen_topic] += 1

    return selected


def generate_hybrid(
    user,
    interactions,
    all_users_interactions,
    resources,
    mastery,
    top_k=None,
    variant: str = HYBRID_VARIANT_FULL,
):
    """
    Combine content-based (TF-IDF), collaborative (KNN cosine), and optionally
    EDM difficulty match into a single hybrid score per resource.

    variant:
      - "full": 0.5*content + 0.3*collab + 0.2*difficulty (config weights).

    - ContentScore: TF-IDF cosine similarity to user's completed resources.
    - CollaborativeScore: KNN user-based collaborative filtering.
    - DifficultyMatch (full only): resource difficulty vs suggested level (0–1).
    """
    if variant not in _HYBRID_VARIANTS:
        raise ValueError(f"Unsupported hybrid variant: {variant!r}. Use one of {_HYBRID_VARIANTS}.")

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
    content_map = {r.learningResourceId: r.score for r in content_recs}

    # ---- 2. Collaborative scores (KNN cosine) ----
    collab_recs = generate_collaborative(
        user_id,
        all_users_interactions,
        resources,
        top_k=len(resources),
    )
    collab_map = {r.learningResourceId: r.score for r in collab_recs}

    # ---- 3. EDM difficulty: suggested level 1–5 ----
    # If EDM mastery is missing, fall back to user's preferredDifficulty.
    suggested_difficulty = None
    if mastery:
        suggested_difficulty = mastery.get("suggestedDifficulty", None)

    # Fallback to user preference if no mastery yet
    if suggested_difficulty is None and user.get("preferences"):
        pref = user["preferences"].get("preferredDifficulty")
        if pref is not None:
            suggested_difficulty = pref

    if suggested_difficulty is None:
        suggested_difficulty = 1

    suggested_difficulty = max(1, min(5, int(suggested_difficulty)))

    # ---- 4. Normalize content and collab to [0, 1] ----
    max_content = max(content_map.values(), default=1) or 1
    max_collab = max(collab_map.values(), default=1) or 1
    popularity_map = _build_resource_popularity(all_users_interactions)
    max_popularity = max(popularity_map.values(), default=0)
    preferred_topics = _parse_preferred_topics(user)

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
        popularity = popularity_map.get(str(rid), 0)
        novelty_s = (1.0 - (popularity / max_popularity)) if max_popularity > 0 else 0.0

        # DifficultyMatch: 1 when resource difficulty == suggested; 0 when |diff| >= 4
        diff_gap = min(4, abs(difficulty - suggested_difficulty))
        difficulty_match = 1.0 - (diff_gap / 4.0)

        if variant == HYBRID_VARIANT_FULL:
            final_score = (
                HYBRID_CONTENT_WEIGHT * content_s
                + HYBRID_COLLAB_WEIGHT * collab_s
                + HYBRID_DIFFICULTY_WEIGHT * difficulty_match
            )
        else:
            final_score = (
                HYBRID_NO_DIFF_CONTENT_WEIGHT * content_s
                + HYBRID_NO_DIFF_COLLAB_WEIGHT * collab_s
            )

        scored.append({
            "resource": resource,
            "final_score": final_score,
            "content_s": content_s,
            "collab_s": collab_s,
            "difficulty_match": difficulty_match,
            "novelty_s": novelty_s,
        })

    # Sort by base score first for stable reranking behavior.
    scored.sort(key=lambda x: x["final_score"], reverse=True)
    top = _rerank_for_diversity(scored, top_k=top_k, preferred_topics=preferred_topics)

    recommendations: list[RecommendationItem] = []
    algorithm_used = _ALGORITHM_BY_VARIANT[variant]

    for item in top:
        r = item["resource"]
        # Clamp to [0, 1] for backend contract (floating point safety)
        final = max(0.0, min(1.0, item["final_score"]))
        if variant == HYBRID_VARIANT_FULL:
            explanation = (
                f"Content match {item['content_s']:.2f}, "
                f"similar users {item['collab_s']:.2f}, "
                f"difficulty fit {item['difficulty_match']:.2f} (suggested level {suggested_difficulty})."
            )
        else:
            explanation = (
                f"Content match {item['content_s']:.2f}, similar users {item['collab_s']:.2f}. "
                "Difficulty is not used as a scoring factor in this variant."
            )

        recommendations.append(
            RecommendationItem(
                learningResourceId=str(r["id"]),
                score=round(final, 4),
                algorithmUsed=algorithm_used,
                explanation=explanation,
            )
        )

    return recommendations
