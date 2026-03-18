from typing import Any, List

import requests

from api.exceptions import BackendError
from config import BACKEND_BASE_URL
from models.recommendation_models import (
    BackendRecommendationsResponse,
    RecommendationBatch,
    RecommendationItem,
)

# Timeout for all backend HTTP calls (seconds)
REQUEST_TIMEOUT = 30


def _backend_call(
    method: str,
    url: str,
    *,
    json_body: Any = None,
    timeout: int = REQUEST_TIMEOUT,
) -> requests.Response:
    """
    Perform a backend HTTP call and raise BackendError on failure.
    Centralizes timeout, connection, and HTTP error handling.
    """
    try:
        if method == "GET":
            r = requests.get(url, timeout=timeout)
        elif method == "POST":
            r = requests.post(url, json=json_body, timeout=timeout)
        else:
            raise ValueError(f"Unsupported method: {method}")

        r.raise_for_status()
        return r

    except requests.Timeout as e:
        raise BackendError(
            504,
            "Backend request timed out.",
        ) from e
    except requests.ConnectionError as e:
        raise BackendError(
            502,
            "Could not reach backend service.",
        ) from e
    except requests.HTTPError as e:
        status = e.response.status_code if e.response is not None else 500
        detail = "Backend error."
        if e.response is not None:
            try:
                body = e.response.json()
                if isinstance(body, dict) and "error" in body:
                    detail = body["error"]
                elif isinstance(body, dict) and "Error" in body:
                    detail = body["Error"]
            except Exception:
                pass
        # Map backend 5xx to 502 so we don't expose internal backend status
        if status >= 500:
            status = 502
            if detail == "Backend error.":
                detail = "Backend unavailable or error."
        raise BackendError(status, detail) from e
    except requests.RequestException as e:
        raise BackendError(
            502,
            "Backend request failed.",
        ) from e


def get_user(user_id: str) -> dict:
    """Get user profile and preferences (for content/difficulty adaptation)."""
    r = _backend_call("GET", f"{BACKEND_BASE_URL}/api/users/{user_id}")
    return r.json()


def get_user_interactions(user_id: str) -> list:
    """Get interactions for a single user (for content-based and filtering completed)."""
    r = _backend_call(
        "GET",
        f"{BACKEND_BASE_URL}/api/users/{user_id}/interactions",
    )
    return r.json()


def get_all_interactions() -> list:
    """
    Get all users' interactions (no query params).
    Required for collaborative filtering user–resource matrix.
    """
    r = _backend_call("GET", f"{BACKEND_BASE_URL}/api/interactions")
    return r.json()


def get_resources() -> list:
    """Get full learning resource catalog."""
    r = _backend_call("GET", f"{BACKEND_BASE_URL}/api/resources")
    return r.json()


def get_user_mastery(user_id: str) -> dict:
    """
    Get EDM mastery: suggested difficulty (1–5) and per-topic mastery.
    Used for difficulty adaptation in hybrid scoring.
    """
    r = _backend_call(
        "GET",
        f"{BACKEND_BASE_URL}/api/users/{user_id}/mastery",
    )
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
    r = _backend_call(
        "POST",
        f"{BACKEND_BASE_URL}/api/users/{user_id}/recommendations",
        json_body=batch.model_dump(),
    )
    return BackendRecommendationsResponse.model_validate(r.json())
