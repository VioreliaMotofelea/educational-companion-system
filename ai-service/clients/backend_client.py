import threading
from typing import Any, List

import requests

from api.exceptions import BackendError
from config import (
    BACKEND_BASE_URL,
    BACKEND_BULK_GET_TIMEOUT_S,
    BACKEND_DISABLE_DATA_CACHE,
    BACKEND_REQUEST_TIMEOUT_S,
)
from models.recommendation_models import (
    BackendRecommendationsResponse,
    RecommendationBatch,
    RecommendationItem,
)

_resources_cache: list | None = None
_backend_bulk_cache_lock = threading.Lock()


def clear_backend_data_cache() -> None:
    global _resources_cache
    with _backend_bulk_cache_lock:
        _resources_cache = None
    try:
        from recommender.collaborative import clear_collaborative_matrix_cache
        from recommender.content_based import clear_content_based_tfidf_cache
        from recommender.content_semantic import clear_content_semantic_cache

        clear_collaborative_matrix_cache()
        clear_content_based_tfidf_cache()
        clear_content_semantic_cache()
    except ImportError:
        pass


def _backend_call(
    method: str,
    url: str,
    *,
    json_body: Any = None,
    timeout: int = BACKEND_REQUEST_TIMEOUT_S,
) -> requests.Response:
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

    Not cached: interactions change when learners complete resources during a session.
    """
    r = _backend_call(
        "GET",
        f"{BACKEND_BASE_URL}/api/interactions",
        timeout=BACKEND_BULK_GET_TIMEOUT_S,
    )
    return r.json()


def get_accessible_resources(user_id: str) -> list:
    """Get learning resources the user may receive in recommendations (visibility + scopes)."""
    r = _backend_call(
        "GET",
        f"{BACKEND_BASE_URL}/api/users/{user_id}/resources/accessible",
        timeout=BACKEND_BULK_GET_TIMEOUT_S,
    )
    return r.json()


def get_resources() -> list:
    """Get full learning resource catalog (admin/evaluation only — not for user recommendation ranking)."""
    global _resources_cache
    if not BACKEND_DISABLE_DATA_CACHE and _resources_cache is not None:
        return _resources_cache
    with _backend_bulk_cache_lock:
        if not BACKEND_DISABLE_DATA_CACHE and _resources_cache is not None:
            return _resources_cache
        r = _backend_call(
            "GET",
            f"{BACKEND_BASE_URL}/api/resources",
            timeout=BACKEND_BULK_GET_TIMEOUT_S,
        )
        data = r.json()
        if not BACKEND_DISABLE_DATA_CACHE:
            _resources_cache = data
        return data


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
