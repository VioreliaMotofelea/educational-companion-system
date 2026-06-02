"""Tests for evaluation-time content mode overrides."""


def test_content_overrides_force_tfidf_despite_env_semantic(monkeypatch):
    import recommender.hybrid as hybrid_mod
    from recommender.content_overrides import ContentModeOverrides

    monkeypatch.setattr(hybrid_mod, "SEMANTIC_CONTENT_ENABLED", True)
    monkeypatch.setattr(hybrid_mod, "CONTENT_FUSION_MODE", "semantic_only")

    tfidf_calls = {"n": 0}
    semantic_calls = {"n": 0}

    def fake_tfidf(*args, **kwargs):
        tfidf_calls["n"] += 1
        return {"r2": 0.7}

    def fake_semantic(*args, **kwargs):
        semantic_calls["n"] += 1
        return {}

    monkeypatch.setattr(hybrid_mod, "compute_tfidf_score_map", fake_tfidf)
    monkeypatch.setattr(hybrid_mod, "compute_semantic_score_map", fake_semantic)
    monkeypatch.setattr(hybrid_mod, "build_collaborative_score_map", lambda *args, **kwargs: {})

    overrides = ContentModeOverrides(semantic_enabled=False, content_fusion_mode="tfidf_only")
    recs = hybrid_mod.generate_hybrid(
        user={"userId": "u1"},
        interactions=[{"learningResourceId": "r1", "interactionType": "Completed"}],
        all_users_interactions=[],
        resources=[{"id": "r1", "difficulty": 2}, {"id": "r2", "difficulty": 2}],
        mastery={"suggestedDifficulty": 2},
        top_k=1,
        variant="full",
        content_overrides=overrides,
    )

    assert tfidf_calls["n"] == 1
    assert semantic_calls["n"] == 0
    assert recs[0].algorithmUsed == "Hybrid-full"


def test_overrides_for_fusion_mode_semantic_only():
    from recommender.content_overrides import overrides_for_fusion_mode

    o = overrides_for_fusion_mode("semantic_only", semantic_model_name="test-model")
    assert o.resolved_semantic_enabled() is True
    assert o.resolved_fusion_mode() == "semantic_only"
    assert o.resolved_semantic_model_name() == "test-model"


def test_generate_hybrid_without_overrides_unchanged(monkeypatch):
    import recommender.hybrid as hybrid_mod

    monkeypatch.setattr(hybrid_mod, "SEMANTIC_CONTENT_ENABLED", False)

    monkeypatch.setattr(
        hybrid_mod,
        "compute_tfidf_score_map",
        lambda *args, **kwargs: {"r2": 0.5},
    )
    monkeypatch.setattr(hybrid_mod, "build_collaborative_score_map", lambda *args, **kwargs: {})

    recs = hybrid_mod.generate_hybrid(
        user={"userId": "u1"},
        interactions=[{"learningResourceId": "r1", "interactionType": "Completed"}],
        all_users_interactions=[],
        resources=[{"id": "r1", "difficulty": 1}, {"id": "r2", "difficulty": 1}],
        mastery=None,
        top_k=1,
        variant="full",
    )

    assert recs[0].algorithmUsed == "Hybrid-full"
