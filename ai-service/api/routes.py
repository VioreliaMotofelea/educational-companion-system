import logging
from typing import Annotated, Literal

from fastapi import APIRouter, HTTPException, Query

from api.exceptions import BackendError
from clients.backend_client import (
    get_user,
    get_user_interactions,
    get_all_interactions,
    get_accessible_resources,
    get_resources,
    get_user_mastery,
    push_recommendations,
)
from evaluation.evaluator import Evaluator
from evaluation.tracking import (
    append_recommendation_session,
    load_logs,
    register_click,
    register_completion,
)
from models.evaluation_models import (
    EvaluationReportResponse,
    InteractionEventRequest,
    InteractionEventResponse,
)
from models.recommendation_models import RecommendationGenerationResponse
from recommender.hybrid import generate_hybrid

router = APIRouter()
logger = logging.getLogger(__name__)


HybridVariant = Literal["full", "no_difficulty"]


@router.post("/generate/{user_id}", response_model=RecommendationGenerationResponse)
def generate_recommendations(
    user_id: str,
    variant: Annotated[
        HybridVariant,
        Query(
            description='Hybrid variant: "full" (content+collab+difficulty) or '
            '"no_difficulty" (content+collab only, renormalized weights).',
        ),
    ] = "full",
):

    logger.info("Generating recommendations for user_id=%s variant=%s", user_id, variant)

    user = get_user(user_id)
    interactions = get_user_interactions(user_id)
    all_users_interactions = get_all_interactions()
    resources = get_accessible_resources(user_id)
    if not resources:
        logger.info("No accessible resources for user_id=%s; returning empty recommendations", user_id)
        result = push_recommendations(user_id, [])
        return RecommendationGenerationResponse(
            userId=user_id,
            generated=0,
            variant=variant,
            backendResponse=result,
        )

    try:
        mastery = get_user_mastery(user_id)
    except BackendError:
        logger.info("No mastery data for user_id=%s, using fallback", user_id)
        mastery = None  # New user or no mastery yet; optional data

    recommendations = generate_hybrid(
        user,
        interactions,
        all_users_interactions,
        resources,
        mastery,
        variant=variant,
    )

    result = push_recommendations(user_id, recommendations)
    append_recommendation_session(
        user_id,
        [r.learningResourceId for r in recommendations],
        variant=variant,
    )

    logger.info(
        "Generated %d recommendations for user_id=%s variant=%s",
        len(recommendations),
        user_id,
        variant,
    )
    return RecommendationGenerationResponse(
        userId=user_id,
        generated=len(recommendations),
        variant=variant,
        backendResponse=result,
    )


@router.post("/evaluation/click", response_model=InteractionEventResponse)
def register_recommendation_click(payload: InteractionEventRequest):
    found = register_click(payload.user_id, payload.item_id)
    if not found:
        raise HTTPException(
            status_code=404,
            detail="No recommendation session found for this user/item.",
        )
    return InteractionEventResponse(
        message="Click event recorded.",
        user_id=payload.user_id,
        item_id=payload.item_id,
    )


@router.post("/evaluation/completion", response_model=InteractionEventResponse)
def register_recommendation_completion(payload: InteractionEventRequest):
    found = register_completion(payload.user_id, payload.item_id)
    if not found:
        raise HTTPException(
            status_code=404,
            detail="No recommendation session found for this user/item.",
        )
    return InteractionEventResponse(
        message="Completion event recorded.",
        user_id=payload.user_id,
        item_id=payload.item_id,
    )


@router.get("/evaluation/report", response_model=EvaluationReportResponse)
def get_evaluation_report(k: int = 5):
    logs = [log.model_dump() for log in load_logs()]
    resources = get_resources()
    total_items = len(resources)
    resource_by_id = {str(r.get("id")): r for r in resources if r.get("id") is not None}
    all_interactions = get_all_interactions()
    interaction_counts = {}
    for interaction in all_interactions:
        item_id = interaction.get("learningResourceId")
        if item_id is None:
            continue
        item_key = str(item_id)
        interaction_counts[item_key] = interaction_counts.get(item_key, 0) + 1

    evaluator = Evaluator(k=k)
    results = evaluator.evaluate(
        logs,
        total_items=total_items,
        resource_by_id=resource_by_id,
        interaction_counts=interaction_counts,
        total_interactions=len(all_interactions),
    )

    return EvaluationReportResponse(
        k=k,
        logs_count=len(logs),
        precision_at_k=results.get("precision@k", 0.0),
        recall_at_k=results.get("recall@k", 0.0),
        ndcg_at_k=results.get("ndcg@k", 0.0),
        ctr=results.get("ctr", 0.0),
        completion_rate=results.get("completion_rate", 0.0),
        coverage=results.get("coverage", 0.0),
        diversity=results.get("diversity", 0.0),
        novelty=results.get("novelty", 0.0),
    )