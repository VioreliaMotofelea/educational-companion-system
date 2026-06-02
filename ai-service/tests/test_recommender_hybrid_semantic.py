import pytest


def test_hybrid_default_remains_tfidf_only(monkeypatch):
    import recommender.hybrid as hybrid_mod

    monkeypatch.setattr(hybrid_mod, "SEMANTIC_CONTENT_ENABLED", False)

    resources = [{"id": "r1", "difficulty": 1}, {"id": "r2", "difficulty": 1}]
    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]

    calls = {"semantic": 0}

    def fake_semantic(*args, **kwargs):
        calls["semantic"] += 1
        return {}

    monkeypatch.setattr(hybrid_mod, "compute_tfidf_score_map", lambda *args, **kwargs: {"r2": 0.5})
    monkeypatch.setattr(hybrid_mod, "compute_semantic_score_map", fake_semantic)
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

    assert calls["semantic"] == 0
    assert recs[0].algorithmUsed == "Hybrid-full"
    assert "Semantic" not in recs[0].explanation


def test_hybrid_semantic_only_uses_semantic_scores(monkeypatch):
    import recommender.hybrid as hybrid_mod

    monkeypatch.setattr(hybrid_mod, "SEMANTIC_CONTENT_ENABLED", True)
    monkeypatch.setattr(hybrid_mod, "CONTENT_FUSION_MODE", "semantic_only")

    resources = [{"id": "r1", "difficulty": 1}, {"id": "r2", "difficulty": 1}]
    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]

    monkeypatch.setattr(
        hybrid_mod,
        "compute_tfidf_score_map",
        lambda *args, **kwargs: pytest.fail("TF-IDF should not run in semantic_only mode"),
    )
    monkeypatch.setattr(
        hybrid_mod,
        "compute_semantic_score_map",
        lambda *args, **kwargs: {"r2": 0.9},
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

    assert recs[0].algorithmUsed == "Hybrid-full+semantic"
    assert recs[0].explanation.startswith("Semantic content match")


def test_hybrid_tfidf_semantic_fusion(monkeypatch):
    import recommender.hybrid as hybrid_mod

    monkeypatch.setattr(hybrid_mod, "SEMANTIC_CONTENT_ENABLED", True)
    monkeypatch.setattr(hybrid_mod, "CONTENT_FUSION_MODE", "tfidf_semantic")
    monkeypatch.setattr(hybrid_mod, "CONTENT_TFIDF_SUBWEIGHT", 0.5)
    monkeypatch.setattr(hybrid_mod, "CONTENT_SEMANTIC_SUBWEIGHT", 0.5)

    resources = [{"id": "r1", "difficulty": 1}, {"id": "r2", "difficulty": 1}]
    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]

    monkeypatch.setattr(
        hybrid_mod,
        "compute_tfidf_score_map",
        lambda *args, **kwargs: {"r2": 1.0},
    )
    monkeypatch.setattr(
        hybrid_mod,
        "compute_semantic_score_map",
        lambda *args, **kwargs: {"r2": 0.0},
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

    assert recs[0].algorithmUsed == "Hybrid-full+semantic"
    assert "TF-IDF+semantic" in recs[0].explanation
