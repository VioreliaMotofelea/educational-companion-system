from collections import Counter

from config import (
    CONTENT_FUSION_MODE,
    CONTENT_SEMANTIC_SUBWEIGHT,
    CONTENT_TFIDF_SUBWEIGHT,
    HYBRID_CONTENT_WEIGHT,
    HYBRID_COLLAB_WEIGHT,
    HYBRID_DIFFICULTY_WEIGHT,
    HYBRID_NO_DIFF_CONTENT_WEIGHT,
    HYBRID_NO_DIFF_COLLAB_WEIGHT,
    HYBRID_TOPIC_PREFERENCE_BONUS,
    HYBRID_NOVELTY_BONUS,
    HYBRID_TOPIC_REPEAT_PENALTY,
    DEFAULT_RECOMMENDATION_LIMIT,
    SEMANTIC_CONTENT_ENABLED,
    SEMANTIC_MODEL_NAME,
)
from models.recommendation_models import RecommendationItem
from recommender.content_based import compute_tfidf_score_map, prewarm_tfidf_matrix
from recommender.content_semantic import compute_semantic_score_map
from recommender.collaborative import (
    collaborative_score_map_from_prepared,
    prepare_collaborative_matrix,
)
from recommender.content_overrides import ContentModeOverrides


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
    popularity: Counter[str] = Counter()
    for row in all_users_interactions:
        if row.get("interactionType") != "Completed":
            continue
        rid = row.get("learningResourceId")
        if rid:
            popularity[str(rid)] += 1
    return dict(popularity)


def build_global_hybrid_context(
    all_users_interactions: list,
    resources: list,
) -> tuple[dict[str, int], int]:
    popularity_map = _build_resource_popularity(all_users_interactions)
    max_popularity = max(popularity_map.values(), default=0)
    return popularity_map, max_popularity


def _resolve_suggested_difficulty(user: dict, mastery) -> int:
    suggested_difficulty = None
    if mastery:
        suggested_difficulty = mastery.get("suggestedDifficulty", None)
    if suggested_difficulty is None and user.get("preferences"):
        pref = user["preferences"].get("preferredDifficulty")
        if pref is not None:
            suggested_difficulty = pref
    if suggested_difficulty is None:
        suggested_difficulty = 1
    return max(1, min(5, int(suggested_difficulty)))


def rank_hybrid_from_score_maps(
    *,
    user: dict,
    resources: list,
    completed_ids: set[str],
    content_map: dict[str, float],
    content_mode: str,
    collab_map: dict[str, float],
    popularity_map: dict[str, int],
    max_popularity: int,
    suggested_difficulty: int,
    variant: str,
    top_k: int,
    ids_only: bool = False,
) -> list[RecommendationItem] | list[str]:
    if variant not in _HYBRID_VARIANTS:
        raise ValueError(f"Unsupported hybrid variant: {variant!r}. Use one of {_HYBRID_VARIANTS}.")

    max_content = max(content_map.values(), default=1) or 1
    max_collab = max(collab_map.values(), default=1) or 1
    preferred_topics = _parse_preferred_topics(user)

    candidates = [r for r in resources if str(r["id"]) not in completed_ids]
    scored: list[dict] = []

    for resource in candidates:
        rid = str(resource["id"])
        difficulty = resource.get("difficulty", 1)
        content_s = content_map.get(rid, 0.0) / max_content
        collab_s = collab_map.get(rid, 0.0) / max_collab
        popularity = popularity_map.get(rid, 0)
        novelty_s = (1.0 - (popularity / max_popularity)) if max_popularity > 0 else 0.0
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

    scored.sort(key=lambda x: x["final_score"], reverse=True)
    top = _rerank_for_diversity(scored, top_k=top_k, preferred_topics=preferred_topics)

    algorithm_used = _algorithm_label(variant, content_mode)

    if ids_only:
        return [str(item["resource"]["id"]) for item in top]

    recommendations: list[RecommendationItem] = []

    for item in top:
        r = item["resource"]
        final = max(0.0, min(1.0, item["final_score"]))
        content_prefix = _content_explanation_prefix(content_mode, item["content_s"])

        if variant == HYBRID_VARIANT_FULL:
            explanation = (
                f"{content_prefix}, "
                f"similar users {item['collab_s']:.2f}, "
                f"difficulty fit {item['difficulty_match']:.2f} (suggested level {suggested_difficulty})."
            )
        else:
            explanation = (
                f"{content_prefix}, similar users {item['collab_s']:.2f}. "
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


def build_collaborative_score_map(
    user_id: str,
    all_users_interactions: list,
    resources: list,
    *,
    prepared: tuple | None = None,
) -> dict[str, float]:
    user_id = str(user_id)
    if prepared is not None:
        matrix, all_vecs = prepared
    else:
        matrix, all_vecs = prepare_collaborative_matrix(all_users_interactions, resources)
    if matrix.empty:
        return {}
    return collaborative_score_map_from_prepared(
        user_id,
        matrix,
        all_vecs,
        top_k=len(resources),
    )


def build_all_fusion_content_maps(
    user,
    interactions,
    resources,
    *,
    semantic_model_name: str,
    tfidf_subweight: float,
    semantic_subweight: float,
) -> dict[str, tuple[dict[str, float], str]]:
    tfidf_map = compute_tfidf_score_map(interactions, resources)
    semantic_map = compute_semantic_score_map(
        interactions, resources, model_name=semantic_model_name
    )

    max_tfidf = max(tfidf_map.values(), default=0.0) or 1.0
    max_semantic = max(semantic_map.values(), default=0.0) or 1.0
    weight_sum = tfidf_subweight + semantic_subweight
    if weight_sum <= 0:
        weight_sum = 1.0

    fused: dict[str, float] = {}
    for resource in resources:
        rid = str(resource["id"])
        tfidf_norm = tfidf_map.get(rid, 0.0) / max_tfidf
        semantic_norm = semantic_map.get(rid, 0.0) / max_semantic
        fused[rid] = (
            tfidf_subweight * tfidf_norm + semantic_subweight * semantic_norm
        ) / weight_sum

    return {
        "tfidf_only": (tfidf_map, "tfidf"),
        "semantic_only": (semantic_map, "semantic"),
        "tfidf_semantic": (fused, "tfidf_semantic"),
    }


def _effective_content_fusion_mode(
    content_overrides: ContentModeOverrides | None = None,
) -> str:
    """When semantic is disabled, always use TF-IDF-only content scoring."""
    if content_overrides is not None:
        return content_overrides.resolved_fusion_mode()
    if not SEMANTIC_CONTENT_ENABLED:
        return "tfidf_only"
    return CONTENT_FUSION_MODE


def _build_content_score_map(
    user,
    interactions,
    resources,
    content_overrides: ContentModeOverrides | None = None,
) -> tuple[dict[str, float], str]:
    _ = user  # same signature as standalone content recommenders; profile is from interactions
    mode = _effective_content_fusion_mode(content_overrides)
    semantic_model_name = (
        content_overrides.resolved_semantic_model_name()
        if content_overrides is not None
        else SEMANTIC_MODEL_NAME
    )
    tfidf_subweight = (
        content_overrides.resolved_tfidf_subweight()
        if content_overrides is not None
        else CONTENT_TFIDF_SUBWEIGHT
    )
    semantic_subweight = (
        content_overrides.resolved_semantic_subweight()
        if content_overrides is not None
        else CONTENT_SEMANTIC_SUBWEIGHT
    )

    if mode == "tfidf_only":
        return compute_tfidf_score_map(interactions, resources), "tfidf"

    if mode == "semantic_only":
        return (
            compute_semantic_score_map(
                interactions, resources, model_name=semantic_model_name
            ),
            "semantic",
        )

    tfidf_map = compute_tfidf_score_map(interactions, resources)
    semantic_map = compute_semantic_score_map(
        interactions, resources, model_name=semantic_model_name
    )

    max_tfidf = max(tfidf_map.values(), default=0.0) or 1.0
    max_semantic = max(semantic_map.values(), default=0.0) or 1.0
    weight_sum = tfidf_subweight + semantic_subweight
    if weight_sum <= 0:
        weight_sum = 1.0

    fused: dict[str, float] = {}
    for resource in resources:
        rid = str(resource["id"])
        tfidf_norm = tfidf_map.get(rid, 0.0) / max_tfidf
        semantic_norm = semantic_map.get(rid, 0.0) / max_semantic
        fused[rid] = (
            tfidf_subweight * tfidf_norm
            + semantic_subweight * semantic_norm
        ) / weight_sum

    return fused, "tfidf_semantic"


def _algorithm_label(variant: str, content_mode: str) -> str:
    base = _ALGORITHM_BY_VARIANT[variant]
    if content_mode in ("semantic", "tfidf_semantic"):
        return f"{base}+semantic"
    return base


def _content_explanation_prefix(content_mode: str, content_s: float) -> str:
    if content_mode == "semantic":
        return f"Semantic content match {content_s:.2f}"
    if content_mode == "tfidf_semantic":
        return f"Content match {content_s:.2f} (TF-IDF+semantic)"
    return f"Content match {content_s:.2f}"


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
    content_overrides: ContentModeOverrides | None = None,
):
    """
    Combine content-based (TF-IDF and/or semantic), collaborative (KNN cosine), and
    optionally EDM difficulty match into a single hybrid score per resource.

    variant:
      - "full": 0.5*content + 0.3*collab + 0.2*difficulty (config weights).
      - "no_difficulty": renormalized content/collab weights only.

    Content-based is TF-IDF-only by default, semantic embeddings via config.
    """
    if variant not in _HYBRID_VARIANTS:
        raise ValueError(f"Unsupported hybrid variant: {variant!r}. Use one of {_HYBRID_VARIANTS}.")

    top_k = top_k or DEFAULT_RECOMMENDATION_LIMIT
    user_id = user.get("userId")

    completed_ids = {
        str(i["learningResourceId"])
        for i in interactions
        if i.get("interactionType") in ("Completed", "Skipped")
    }

    content_map, content_mode = _build_content_score_map(
        user, interactions, resources, content_overrides=content_overrides
    )
    collab_map = build_collaborative_score_map(str(user_id), all_users_interactions, resources)
    popularity_map, max_popularity = build_global_hybrid_context(all_users_interactions, resources)
    suggested_difficulty = _resolve_suggested_difficulty(user, mastery)

    return rank_hybrid_from_score_maps(
        user=user,
        resources=resources,
        completed_ids=completed_ids,
        content_map=content_map,
        content_mode=content_mode,
        collab_map=collab_map,
        popularity_map=popularity_map,
        max_popularity=max_popularity,
        suggested_difficulty=suggested_difficulty,
        variant=variant,
        top_k=top_k,
    )
