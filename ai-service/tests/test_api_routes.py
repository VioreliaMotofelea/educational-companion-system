import pytest
from fastapi import HTTPException

from api.exceptions import BackendError
from models.evaluation_models import RecommendationLog
from models.evaluation_models import InteractionEventRequest
from models.recommendation_models import (
    BackendRecommendationsResponse,
    RecommendationItem,
)

import api.routes as routes_mod


def _patch_generate_dependencies(
    monkeypatch,
    *,
    user_id: str,
    accessible_resources: list,
    mastery=None,
    mastery_raises_404: bool = False,
):
    """Patch generate flow; uses accessible resources only (not full catalog)."""

    def fake_get_user(uid: str):
        return {"userId": uid, "preferences": {"preferredDifficulty": 3}}

    def fake_get_user_interactions(uid: str):
        return [{"learningResourceId": "r1", "interactionType": "Completed"}]

    def fake_get_all_interactions():
        return []

    def fake_get_accessible_resources(uid: str):
        assert uid == user_id
        return accessible_resources

    def fake_get_resources():
        raise AssertionError("generate_recommendations must not call get_resources()")

    def fake_get_user_mastery(uid: str):
        if mastery_raises_404:
            raise BackendError(404, "No mastery")
        return mastery if mastery is not None else {"suggestedDifficulty": 2}

    monkeypatch.setattr(routes_mod, "get_user", fake_get_user)
    monkeypatch.setattr(routes_mod, "get_user_interactions", fake_get_user_interactions)
    monkeypatch.setattr(routes_mod, "get_all_interactions", fake_get_all_interactions)
    monkeypatch.setattr(routes_mod, "get_accessible_resources", fake_get_accessible_resources)
    monkeypatch.setattr(routes_mod, "get_resources", fake_get_resources)
    monkeypatch.setattr(routes_mod, "get_user_mastery", fake_get_user_mastery)


def test_generate_recommendations_calls_dependencies(monkeypatch):
    captured = {"append_called": False, "append_items": None}

    _patch_generate_dependencies(
        monkeypatch,
        user_id="user-2",
        accessible_resources=[
            {"id": "r1", "difficulty": 1},
            {"id": "r2", "difficulty": 2},
        ],
    )

    def fake_generate_hybrid(user, interactions, all_users_interactions, resources, mastery, top_k=None, variant="full"):
        assert mastery["suggestedDifficulty"] == 2
        assert variant == "full"
        return [
            RecommendationItem(
                learningResourceId="r2",
                score=0.9,
                algorithmUsed="Hybrid-full",
                explanation="ok",
            )
        ]

    def fake_push_recommendations(user_id: str, recommendations):
        return BackendRecommendationsResponse(
            userId=user_id,
            createdCount=len(recommendations),
            replacedExisting=True,
        )

    def fake_append_recommendation_session(user_id: str, recommended_items, variant=None):
        captured["append_called"] = True
        captured["append_items"] = recommended_items
        captured["variant"] = variant
        return RecommendationLog(user_id=user_id, recommended_items=recommended_items, variant=variant)

    monkeypatch.setattr(routes_mod, "generate_hybrid", fake_generate_hybrid)
    monkeypatch.setattr(routes_mod, "push_recommendations", fake_push_recommendations)
    monkeypatch.setattr(routes_mod, "append_recommendation_session", fake_append_recommendation_session)

    res = routes_mod.generate_recommendations("user-2", variant="full")
    assert res.userId == "user-2"
    assert res.generated == 1
    assert res.variant == "full"
    assert res.backendResponse.createdCount == 1
    assert captured["append_called"] is True
    assert captured["append_items"] == ["r2"]
    assert captured["variant"] == "full"


def test_generate_recommendations_falls_back_when_mastery_missing(monkeypatch):
    _patch_generate_dependencies(
        monkeypatch,
        user_id="user-2",
        accessible_resources=[
            {"id": "r1", "difficulty": 1},
            {"id": "r2", "difficulty": 2},
        ],
        mastery_raises_404=True,
    )

    def fake_generate_hybrid(user, interactions, all_users_interactions, resources, mastery, top_k=None, variant="full"):
        assert mastery is None
        assert variant == "full"
        return [
            RecommendationItem(
                learningResourceId="r2",
                score=0.5,
                algorithmUsed="Hybrid-full",
                explanation="ok",
            )
        ]

    def fake_push_recommendations(user_id: str, recommendations):
        return BackendRecommendationsResponse(
            userId=user_id,
            createdCount=len(recommendations),
            replacedExisting=True,
        )

    def fake_append_recommendation_session(user_id: str, recommended_items, variant=None):
        return RecommendationLog(user_id=user_id, recommended_items=recommended_items, variant=variant)

    monkeypatch.setattr(routes_mod, "generate_hybrid", fake_generate_hybrid)
    monkeypatch.setattr(routes_mod, "push_recommendations", fake_push_recommendations)
    monkeypatch.setattr(routes_mod, "append_recommendation_session", fake_append_recommendation_session)

    res = routes_mod.generate_recommendations("user-2", variant="full")
    assert res.userId == "user-2"
    assert res.variant == "full"


def test_generate_recommendations_passes_no_difficulty_variant(monkeypatch):
    captured = {}

    _patch_generate_dependencies(
        monkeypatch,
        user_id="user-x",
        accessible_resources=[{"id": "r1", "difficulty": 1}],
        mastery={"suggestedDifficulty": 2},
    )

    def fake_get_user(user_id: str):
        return {"userId": user_id}

    def fake_generate_hybrid(user, interactions, all_users_interactions, resources, mastery, top_k=None, variant="full"):
        captured["variant"] = variant
        return [
            RecommendationItem(
                learningResourceId="r1",
                score=0.4,
                algorithmUsed="Hybrid-no_difficulty",
                explanation="ok",
            )
        ]

    def fake_push_recommendations(user_id: str, recommendations):
        return BackendRecommendationsResponse(
            userId=user_id,
            createdCount=len(recommendations),
            replacedExisting=True,
        )

    def fake_append_recommendation_session(user_id: str, recommended_items, variant=None):
        captured["append_variant"] = variant
        return RecommendationLog(user_id=user_id, recommended_items=recommended_items, variant=variant)

    monkeypatch.setattr(routes_mod, "get_user", fake_get_user)
    monkeypatch.setattr(routes_mod, "generate_hybrid", fake_generate_hybrid)
    monkeypatch.setattr(routes_mod, "push_recommendations", fake_push_recommendations)
    monkeypatch.setattr(routes_mod, "append_recommendation_session", fake_append_recommendation_session)

    res = routes_mod.generate_recommendations("user-x", variant="no_difficulty")
    assert res.variant == "no_difficulty"
    assert captured["variant"] == "no_difficulty"
    assert captured["append_variant"] == "no_difficulty"


def test_generate_recommendations_empty_accessible_resources_skips_ranking(monkeypatch):
    _patch_generate_dependencies(monkeypatch, user_id="user-empty", accessible_resources=[])

    hybrid_called = []

    def fake_generate_hybrid(*args, **kwargs):
        hybrid_called.append(True)
        return []

    def fake_push_recommendations(user_id: str, recommendations):
        assert recommendations == []
        return BackendRecommendationsResponse(
            userId=user_id,
            createdCount=0,
            replacedExisting=True,
        )

    monkeypatch.setattr(routes_mod, "generate_hybrid", fake_generate_hybrid)
    monkeypatch.setattr(routes_mod, "push_recommendations", fake_push_recommendations)

    res = routes_mod.generate_recommendations("user-empty", variant="full")
    assert res.generated == 0
    assert hybrid_called == []


def test_evaluation_click_returns_404_when_no_session(monkeypatch):
    monkeypatch.setattr(routes_mod, "register_click", lambda user_id, item_id: False)

    payload = InteractionEventRequest(user_id="user-2", item_id="missing-item")
    with pytest.raises(HTTPException) as excinfo:
        routes_mod.register_recommendation_click(payload)

    assert getattr(excinfo.value, "status_code", None) == 404


def test_evaluation_report_computes_expected_basic_metrics(monkeypatch):
    def fake_load_logs():
        return [
            RecommendationLog(
                user_id="u1",
                recommended_items=["i1", "i2"],
                clicked_items=["i2"],
                completed_items=["i1"],
            )
        ]

    def fake_get_resources():
        return [
            {"id": "i1", "topic": "T1", "contentType": "Article", "difficulty": 1},
            {"id": "i2", "topic": "T2", "contentType": "Video", "difficulty": 3},
            {"id": "i3", "topic": "T2", "contentType": "Article", "difficulty": 5},
        ]

    def fake_get_all_interactions():
        # popularity counts for novelty
        return [
            {"learningResourceId": "i1"},
            {"learningResourceId": "i1"},
            {"learningResourceId": "i2"},
        ]

    monkeypatch.setattr(routes_mod, "load_logs", fake_load_logs)
    monkeypatch.setattr(routes_mod, "get_resources", fake_get_resources)
    monkeypatch.setattr(routes_mod, "get_all_interactions", fake_get_all_interactions)

    res = routes_mod.get_evaluation_report(k=2)

    assert res.k == 2
    assert res.logs_count == 1

    assert res.precision_at_k == 1.0
    assert res.recall_at_k == 1.0
    assert res.ndcg_at_k == 1.0
    assert res.ctr == 0.5
    assert res.completion_rate == 0.5
    assert res.coverage == 2 / 3

    assert 0.0 <= res.diversity <= 1.0
    assert 0.0 <= res.novelty <= 1.0

