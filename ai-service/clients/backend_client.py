import requests
from config import BACKEND_BASE_URL


def get_user(user_id: str):
    """Get user profile and preferences (for content/difficulty adaptation)."""
    r = requests.get(f"{BACKEND_BASE_URL}/api/users/{user_id}")
    r.raise_for_status()
    return r.json()


def get_user_interactions(user_id: str):
    """Get interactions for a single user (for content-based and filtering completed)."""
    r = requests.get(f"{BACKEND_BASE_URL}/api/users/{user_id}/interactions")
    r.raise_for_status()
    return r.json()


def get_all_interactions():
    """
    Get all users' interactions (no query params).
    Required for collaborative filtering user–resource matrix.
    """
    r = requests.get(f"{BACKEND_BASE_URL}/api/interactions")
    r.raise_for_status()
    return r.json()


def get_resources():
    """Get full learning resource catalog."""
    r = requests.get(f"{BACKEND_BASE_URL}/api/resources")
    r.raise_for_status()
    return r.json()


def get_user_mastery(user_id: str):
    """
    Get EDM mastery: suggested difficulty (1–5) and per-topic mastery.
    Used for difficulty adaptation in hybrid scoring.
    """
    r = requests.get(f"{BACKEND_BASE_URL}/api/users/{user_id}/mastery")
    r.raise_for_status()
    return r.json()


def push_recommendations(user_id: str, recommendations):
    """Write recommendations to backend (replace existing for user)."""
    body = {
        "recommendations": recommendations,
        "replaceExisting": True
    }

    r = requests.post(
        f"{BACKEND_BASE_URL}/api/users/{user_id}/recommendations",
        json=body
    )

    r.raise_for_status()
    return r.json()
