import logging

from fastapi import APIRouter

from api.exceptions import BackendError
from clients.backend_client import (
    get_user,
    get_user_interactions,
    get_all_interactions,
    get_resources,
    get_user_mastery,
    push_recommendations,
)
from models.recommendation_models import RecommendationGenerationResponse
from recommender.hybrid import generate_hybrid

router = APIRouter()
logger = logging.getLogger(__name__)


@router.post("/generate/{user_id}", response_model=RecommendationGenerationResponse)
def generate_recommendations(user_id: str):
    """
    Generate hybrid recommendations for a user and persist them to the backend.

    Fetches: user profile, user interactions, all users' interactions (for collaborative),
    resources, and EDM mastery (for difficulty adaptation). Combines TF-IDF content-based,
    KNN collaborative, and difficulty match into final scores and writes to backend.
    """
    logger.info("Generating recommendations for user_id=%s", user_id)

    user = get_user(user_id)
    interactions = get_user_interactions(user_id)
    all_users_interactions = get_all_interactions()
    resources = get_resources()

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
    )

    result = push_recommendations(user_id, recommendations)

    logger.info(
        "Generated %d recommendations for user_id=%s",
        len(recommendations),
        user_id,
    )
    return RecommendationGenerationResponse(
        userId=user_id,
        generated=len(recommendations),
        backendResponse=result,
    )