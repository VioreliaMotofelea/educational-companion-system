from typing import List

import requests

from config import BACKEND_BASE_URL
from models.recommendation_models import (
    BackendRecommendationsResponse,
    RecommendationBatch,
    RecommendationItem,
)

# Timeout for all backend HTTP calls (seconds)
REQUEST_TIMEOUT = 30


def get_user(user_id: str):
    """Get user profile and preferences (for content/difficulty adaptation)."""
    r = requests.get(f"{BACKEND_BASE_URL}/api/users/{user_id}", timeout=REQUEST_TIMEOUT)
    r.raise_for_status()
    return r.json()


def get_user_interactions(user_id: str):
    """Get interactions for a single user (for content-based and filtering completed)."""
    r = requests.get(
        f"{BACKEND_BASE_URL}/api/users/{user_id}/interactions",
        timeout=REQUEST_TIMEOUT,
    )
    r.raise_for_status()
    return r.json()


def get_all_interactions():
    """
    Get all users' interactions (no query params).
    Required for collaborative filtering user–resource matrix.
    """
    r = requests.get(f"{BACKEND_BASE_URL}/api/interactions", timeout=REQUEST_TIMEOUT)
    r.raise_for_status()
    return r.json()


def get_resources():
    """Get full learning resource catalog."""
    r = requests.get(f"{BACKEND_BASE_URL}/api/resources", timeout=REQUEST_TIMEOUT)
    r.raise_for_status()
    return r.json()


def get_user_mastery(user_id: str):
    """
    Get EDM mastery: suggested difficulty (1–5) and per-topic mastery.
    Used for difficulty adaptation in hybrid scoring.
    """
    r = requests.get(
        f"{BACKEND_BASE_URL}/api/users/{user_id}/mastery",
        timeout=REQUEST_TIMEOUT,
    )
    r.raise_for_status()
    return r.json()


def push_recommendations(
    user_id: str,
    recommendations: List[RecommendationItem],
) -> BackendRecommendationsResponse:
    """Write recommendations to backend (replace existing for user)."""
    batch = RecommendationBatch(
        recommendations=recommendations,
        replaceExisting=True,
    )
    r = requests.post(
        f"{BACKEND_BASE_URL}/api/users/{user_id}/recommendations",
        json=batch.model_dump(),
        timeout=REQUEST_TIMEOUT,
    )
    r.raise_for_status()
    return BackendRecommendationsResponse.model_validate(r.json())
