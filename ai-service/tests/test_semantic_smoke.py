"""Lightweight smoke tests for the semantic content-based path."""

import numpy as np
import pytest

from models.recommendation_models import RecommendationItem
from recommender import content_semantic as semantic_mod
from recommender.content_semantic import (
    _dedupe_preserve_order,
    build_resource_semantic_text,
    clear_content_semantic_disk_cache,
    generate_content_semantic,
)
from recommender.hybrid import _build_content_score_map, _effective_content_fusion_mode


def test_semantic_disabled_preserves_tfidf_only_fusion_mode(monkeypatch):
    import recommender.hybrid as hybrid_mod

    monkeypatch.setattr(hybrid_mod, "SEMANTIC_CONTENT_ENABLED", False)
    monkeypatch.setattr(hybrid_mod, "CONTENT_FUSION_MODE", "semantic_only")

    assert _effective_content_fusion_mode() == "tfidf_only"


def test_semantic_disabled_hybrid_uses_tfidf_only(monkeypatch):
    import recommender.hybrid as hybrid_mod

    monkeypatch.setattr(hybrid_mod, "SEMANTIC_CONTENT_ENABLED", False)

    resources = [{"id": "r1", "difficulty": 1}, {"id": "r2", "difficulty": 1}]
    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]
    calls = {"semantic": 0, "tfidf": 0}

    monkeypatch.setattr(
        hybrid_mod,
        "compute_semantic_score_map",
        lambda *args, **kwargs: calls.__setitem__("semantic", calls["semantic"] + 1) or {},
    )
    monkeypatch.setattr(
        hybrid_mod,
        "compute_tfidf_score_map",
        lambda *args, **kwargs: calls.__setitem__("tfidf", calls["tfidf"] + 1) or {"r2": 0.5},
    )
    monkeypatch.setattr(hybrid_mod, "build_collaborative_score_map", lambda *args, **kwargs: {})

    recs = hybrid_mod.generate_hybrid(
        user={"userId": "u1"},
        interactions=interactions,
        all_users_interactions=[],
        resources=resources,
        mastery=None,
        top_k=1,
        variant="full",
    )

    assert calls == {"semantic": 0, "tfidf": 1}
    assert recs[0].algorithmUsed == "Hybrid-full"


def test_build_resource_semantic_text_uses_structured_rich_fields():
    resource = {
        "id": "r1",
        "title": "Intro",
        "topic": "Python",
        "description": "basics",
        "contentType": "Video",
        "content": "Longer lesson body",
        "week": "Week 1",
        "activityType": "oucontent",
        "difficulty": 4,
        "fullContent": "ignored unless mapped to content/body/text",
    }
    text = build_resource_semantic_text(resource)
    assert text.splitlines() == [
        "Title: Intro",
        "Topic: Python",
        "Description: basics",
        "Content: Longer lesson body",
        "Type: Video",
        "Week: Week 1",
        "Activity: oucontent",
    ]
    assert "fullContent" not in text
    assert "difficulty" not in text.lower() or "Difficulty" not in text


def test_semantic_returns_empty_without_completed_interactions():
    resources = [{"id": "r1", "title": "A", "topic": "T", "description": "d"}]
    interactions = [{"learningResourceId": "r1", "interactionType": "Viewed"}]
    assert generate_content_semantic({}, interactions, resources) == []


def test_semantic_id_normalization_mixed_int_and_str(monkeypatch):
    monkeypatch.setattr(
        semantic_mod,
        "_get_resource_embeddings",
        lambda resources, model_name: np.eye(len(resources), dtype=np.float32),
    )

    resources = [
        {"id": 1, "title": "A", "topic": "T", "description": "one"},
        {"id": "2", "title": "B", "topic": "T", "description": "two"},
    ]
    interactions = [{"learningResourceId": "1", "interactionType": "Completed"}]

    recs = generate_content_semantic({}, interactions, resources, top_k=2, model_name="test")
    assert {r.learningResourceId for r in recs} == {"2"}


@pytest.mark.parametrize(
    ("fusion_mode", "expect_tfidf", "expect_semantic"),
    [
        ("tfidf_only", True, False),
        ("semantic_only", False, True),
        ("tfidf_semantic", True, True),
    ],
)
def test_fusion_mode_selection(monkeypatch, fusion_mode, expect_tfidf, expect_semantic):
    import recommender.hybrid as hybrid_mod

    monkeypatch.setattr(hybrid_mod, "SEMANTIC_CONTENT_ENABLED", True)
    monkeypatch.setattr(hybrid_mod, "CONTENT_FUSION_MODE", fusion_mode)

    resources = [{"id": "r1"}, {"id": "r2"}]
    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]
    calls = {"tfidf": 0, "semantic": 0}

    monkeypatch.setattr(
        hybrid_mod,
        "compute_tfidf_score_map",
        lambda *args, **kwargs: calls.__setitem__("tfidf", calls["tfidf"] + 1)
        or {"r2": 0.4},
    )
    monkeypatch.setattr(
        hybrid_mod,
        "compute_semantic_score_map",
        lambda *args, **kwargs: calls.__setitem__("semantic", calls["semantic"] + 1)
        or {"r2": 0.6},
    )

    score_map, mode = _build_content_score_map({}, interactions, resources)

    assert bool(calls["tfidf"]) is expect_tfidf
    assert bool(calls["semantic"]) is expect_semantic
    assert "r2" in score_map
    if fusion_mode == "tfidf_only":
        assert mode == "tfidf"
    elif fusion_mode == "semantic_only":
        assert mode == "semantic"
    else:
        assert mode == "tfidf_semantic"


def test_clear_content_semantic_disk_cache_all_and_per_model(tmp_path, monkeypatch):
    monkeypatch.setattr(semantic_mod, "SEMANTIC_CACHE_DIR", tmp_path)

    model_a = tmp_path / "model-a"
    model_b = tmp_path / "model-b"
    model_a.mkdir(parents=True)
    model_b.mkdir(parents=True)
    (model_a / "cache.npz").write_bytes(b"x")
    (model_b / "cache.npz").write_bytes(b"y")

    clear_content_semantic_disk_cache("model-a")
    assert not model_a.exists()
    assert model_b.exists()

    clear_content_semantic_disk_cache(None)
    assert not tmp_path.exists()


def test_dedupe_preserve_order_keeps_first_seen_ids():
    assert _dedupe_preserve_order(["r1", "r1", "r2", "r1", "r2"]) == ["r1", "r2"]


def test_duplicate_completed_interactions_use_unique_profile(monkeypatch):
    """Repeated Completed rows for the same resource must not inflate its embedding weight."""
    embeddings = np.array(
        [
            [1.0, 0.0],
            [0.0, 1.0],
            [1.0, 1.0],
        ],
        dtype=np.float32,
    )
    monkeypatch.setattr(
        semantic_mod,
        "_get_resource_embeddings",
        lambda resources, model_name: embeddings,
    )

    resources = [
        {"id": "r1", "title": "A", "topic": "T", "description": "one"},
        {"id": "r2", "title": "B", "topic": "T", "description": "two"},
        {"id": "r3", "title": "C", "topic": "T", "description": "three"},
    ]
    duplicate_interactions = [
        {"learningResourceId": "r1", "interactionType": "Completed"},
        {"learningResourceId": "r1", "interactionType": "Completed"},
        {"learningResourceId": "r2", "interactionType": "Completed"},
    ]
    single_interactions = [
        {"learningResourceId": "r1", "interactionType": "Completed"},
        {"learningResourceId": "r2", "interactionType": "Completed"},
    ]

    dup_recs = generate_content_semantic({}, duplicate_interactions, resources, top_k=1, model_name="test")
    single_recs = generate_content_semantic({}, single_interactions, resources, top_k=1, model_name="test")

    assert dup_recs[0].learningResourceId == single_recs[0].learningResourceId == "r3"
    assert dup_recs[0].score == pytest.approx(single_recs[0].score)
