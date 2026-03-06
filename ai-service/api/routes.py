import requests
from fastapi import APIRouter, HTTPException

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


@router.post("/generate/{user_id}", response_model=RecommendationGenerationResponse)
def generate_recommendations(user_id: str):
    """
    Generate hybrid recommendations for a user and persist them to the backend.

    Fetches: user profile, user interactions, all users' interactions (for collaborative),
    resources, and EDM mastery (for difficulty adaptation). Combines TF-IDF content-based,
    KNN collaborative, and difficulty match into final scores and writes to backend.
    """
    try:
        user = get_user(user_id)
    except requests.HTTPError as e:
        if e.response is not None and e.response.status_code == 404:
            raise HTTPException(
                status_code=404,
                detail=f"User not found: {user_id}",
            ) from e
        raise HTTPException(
            status_code=502,
            detail="Backend unavailable or error while fetching user.",
        ) from e
    except requests.RequestException as e:
        raise HTTPException(
            status_code=502,
            detail="Could not reach backend service.",
        ) from e

    interactions = get_user_interactions(user_id)
    all_users_interactions = get_all_interactions()
    resources = get_resources()

    try:
        mastery = get_user_mastery(user_id)
    except Exception:
        mastery = None  # New user or no mastery yet; difficulty uses fallback

    recommendations = generate_hybrid(
        user,
        interactions,
        all_users_interactions,
        resources,
        mastery,
    )

    result = push_recommendations(user_id, recommendations)

    return RecommendationGenerationResponse(
        userId=user_id,
        generated=len(recommendations),
        backendResponse=result,
    )