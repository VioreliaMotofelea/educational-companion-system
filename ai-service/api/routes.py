from fastapi import APIRouter

from clients.backend_client import (
    get_user,
    get_user_interactions,
    get_resources,
    push_recommendations
)

from recommender.hybrid import generate_hybrid

router = APIRouter()

@router.post("/generate/{user_id}")
def generate_recommendations(user_id: str):

    user = get_user(user_id)
    interactions = get_user_interactions(user_id)
    resources = get_resources()

    recommendations = generate_hybrid(user, interactions, resources)

    result = push_recommendations(user_id, recommendations)

    return {
        "userId": user_id,
        "generated": len(recommendations),
        "backendResponse": result
    }