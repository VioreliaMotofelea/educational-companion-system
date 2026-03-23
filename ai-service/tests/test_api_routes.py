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


def test_generate_recommendations_calls_dependencies(monkeypatch):
    captured = {"append_called": False, "append_items": None}

    def fake_get_user(user_id: str):
        return {"userId": user_id, "preferences": {"preferredDifficulty": 3}}

    def fake_get_user_interactions(user_id: str):
        return [
            {"learningResourceId": "r1", "interactionType": "Completed"},
        ]

    def fake_get_all_interactions():
        return []

    def fake_get_resources():
        return [
            {"id": "r1", "difficulty": 1},
            {"id": "r2", "difficulty": 2},
        ]

    def fake_get_user_mastery(user_id: str):
        return {"suggestedDifficulty": 2}

    def fake_generate_hybrid(user, interactions, all_users_interactions, resources, mastery, top_k=None):
        assert mastery["suggestedDifficulty"] == 2
        return [
            RecommendationItem(
                learningResourceId="r2",
                score=0.9,
                algorithmUsed="Hybrid",
                explanation="ok",
            )
        ]

    def fake_push_recommendations(user_id: str, recommendations):
        return BackendRecommendationsResponse(
            userId=user_id,
            createdCount=len(recommendations),
            replacedExisting=True,
        )

    def fake_append_recommendation_session(user_id: str, recommended_items):
        captured["append_called"] = True
        captured["append_items"] = recommended_items
        return RecommendationLog(user_id=user_id, recommended_items=recommended_items)

    monkeypatch.setattr(routes_mod, "get_user", fake_get_user)
    monkeypatch.setattr(routes_mod, "get_user_interactions", fake_get_user_interactions)
    monkeypatch.setattr(routes_mod, "get_all_interactions", fake_get_all_interactions)
    monkeypatch.setattr(routes_mod, "get_resources", fake_get_resources)
    monkeypatch.setattr(routes_mod, "get_user_mastery", fake_get_user_mastery)
    monkeypatch.setattr(routes_mod, "generate_hybrid", fake_generate_hybrid)
    monkeypatch.setattr(routes_mod, "push_recommendations", fake_push_recommendations)
    monkeypatch.setattr(routes_mod, "append_recommendation_session", fake_append_recommendation_session)

    res = routes_mod.generate_recommendations("user-2")
    assert res.userId == "user-2"
    assert res.generated == 1
    assert res.backendResponse.createdCount == 1
    assert captured["append_called"] is True
    assert captured["append_items"] == ["r2"]


def test_generate_recommendations_falls_back_when_mastery_missing(monkeypatch):
    def fake_get_user(user_id: str):
        return {"userId": user_id, "preferences": {"preferredDifficulty": 2}}

    def fake_get_user_interactions(user_id: str):
        return [{"learningResourceId": "r1", "interactionType": "Completed"}]

    def fake_get_all_interactions():
        return []

    def fake_get_resources():
        return [{"id": "r1", "difficulty": 1}, {"id": "r2", "difficulty": 2}]

    def fake_get_user_mastery(user_id: str):
        raise BackendError(404, "No mastery")

    def fake_generate_hybrid(user, interactions, all_users_interactions, resources, mastery, top_k=None):
        assert mastery is None
        return [
            RecommendationItem(
                learningResourceId="r2",
                score=0.5,
                algorithmUsed="Hybrid",
                explanation="ok",
            )
        ]

    def fake_push_recommendations(user_id: str, recommendations):
        return BackendRecommendationsResponse(
            userId=user_id,
            createdCount=len(recommendations),
            replacedExisting=True,
        )

    def fake_append_recommendation_session(user_id: str, recommended_items):
        return RecommendationLog(user_id=user_id, recommended_items=recommended_items)

    monkeypatch.setattr(routes_mod, "get_user", fake_get_user)
    monkeypatch.setattr(routes_mod, "get_user_interactions", fake_get_user_interactions)
    monkeypatch.setattr(routes_mod, "get_all_interactions", fake_get_all_interactions)
    monkeypatch.setattr(routes_mod, "get_resources", fake_get_resources)
    monkeypatch.setattr(routes_mod, "get_user_mastery", fake_get_user_mastery)
    monkeypatch.setattr(routes_mod, "generate_hybrid", fake_generate_hybrid)
    monkeypatch.setattr(routes_mod, "push_recommendations", fake_push_recommendations)
    monkeypatch.setattr(routes_mod, "append_recommendation_session", fake_append_recommendation_session)

    res = routes_mod.generate_recommendations("user-2")
    assert res.userId == "user-2"


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

