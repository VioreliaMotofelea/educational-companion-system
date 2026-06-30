import pytest

from recommender.hybrid import HYBRID_VARIANT_NO_DIFFICULTY, generate_hybrid


def test_hybrid_scoring_and_ranking_with_difficulty(monkeypatch):
    import recommender.hybrid as hybrid_mod

    resources = [
        {"id": "r1", "difficulty": 1},
        {"id": "r2", "difficulty": 1},
        {"id": "r3", "difficulty": 5},
    ]

    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]

    monkeypatch.setattr(
        hybrid_mod,
        "compute_tfidf_score_map",
        lambda interactions, resources: {"r2": 0.2, "r3": 1.0},
    )
    monkeypatch.setattr(
        hybrid_mod,
        "build_collaborative_score_map",
        lambda user_id, all_users_interactions, resources, prepared=None: {
            "r2": 0.0,
            "r3": 0.5,
        },
    )

    recs = generate_hybrid(
        user={"userId": "u1", "preferences": {"preferredDifficulty": 3}},
        interactions=interactions,
        all_users_interactions=[],
        resources=resources,
        mastery={"suggestedDifficulty": 1},
        top_k=2,
        variant="full",
    )

    assert [r.learningResourceId for r in recs] == ["r3", "r2"]

    # Expected final scores (formula in hybrid.py):
    # r2: 0.5*(0.2/1.0) + 0.3*(0/0.5) + 0.2*1 = 0.3
    # r3: 0.5*(1.0/1.0) + 0.3*(0.5/0.5) + 0.2*0 = 0.8
    assert recs[0].score == 0.8
    assert recs[1].score == 0.3
    assert recs[0].algorithmUsed == "Hybrid-full"
    assert "suggested level 1" in recs[0].explanation
    assert "difficulty fit" in recs[0].explanation
    assert 0.0 <= recs[0].score <= 1.0


def test_hybrid_fallback_to_user_preferences_when_no_mastery(monkeypatch):
    import recommender.hybrid as hybrid_mod

    resources = [
        {"id": "r1", "difficulty": 1},
        {"id": "r2", "difficulty": 2},  # matches preferredDifficulty=2
        {"id": "r3", "difficulty": 5},
    ]
    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]

    monkeypatch.setattr(hybrid_mod, "compute_tfidf_score_map", lambda *args, **kwargs: {})
    monkeypatch.setattr(
        hybrid_mod,
        "build_collaborative_score_map",
        lambda *args, **kwargs: {},
    )

    recs = generate_hybrid(
        user={"userId": "u1", "preferences": {"preferredDifficulty": 2}},
        interactions=interactions,
        all_users_interactions=[],
        resources=resources,
        mastery=None,
        top_k=2,
        variant="full",
    )

    # With content_s=0 and collab_s=0, final score is 0.2*difficulty_match
    # r2: difficulty_match=1 (diff_gap=0) => score=0.2
    # r3: difficulty_match=0.25 (diff_gap=3 => 1 - 3/4) => score=0.05
    assert [r.learningResourceId for r in recs] == ["r2", "r3"]
    assert recs[0].score == 0.2
    assert recs[1].score == 0.05
    assert recs[0].algorithmUsed == "Hybrid-full"


def test_hybrid_no_difficulty_renormalized_weights_and_explanation(monkeypatch):
    import recommender.hybrid as hybrid_mod

    resources = [
        {"id": "r1", "difficulty": 1},
        {"id": "r2", "difficulty": 1},
        {"id": "r3", "difficulty": 5},
    ]
    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]

    monkeypatch.setattr(
        hybrid_mod,
        "compute_tfidf_score_map",
        lambda interactions, resources: {"r2": 0.2, "r3": 1.0},
    )
    monkeypatch.setattr(
        hybrid_mod,
        "build_collaborative_score_map",
        lambda user_id, all_users_interactions, resources, prepared=None: {
            "r2": 0.0,
            "r3": 0.5,
        },
    )

    recs = generate_hybrid(
        user={"userId": "u1", "preferences": {"preferredDifficulty": 3}},
        interactions=interactions,
        all_users_interactions=[],
        resources=resources,
        mastery={"suggestedDifficulty": 1},
        top_k=2,
        variant=HYBRID_VARIANT_NO_DIFFICULTY,
    )

    # r3: 0.625*1.0 + 0.375*1.0 = 1.0
    # r2: 0.625*0.2 + 0.375*0 = 0.125
    assert [r.learningResourceId for r in recs] == ["r3", "r2"]
    assert recs[0].score == 1.0
    assert recs[1].score == 0.125
    assert all(r.algorithmUsed == "Hybrid-no_difficulty" for r in recs)
    assert "not used as a scoring factor" in recs[0].explanation
    assert "difficulty fit" not in recs[0].explanation


def test_generate_hybrid_rejects_unknown_variant():
    with pytest.raises(ValueError, match="Unsupported hybrid variant"):
        generate_hybrid(
            {"userId": "u1"},
            [],
            [],
            [{"id": "x", "difficulty": 1}],
            None,
            variant="invalid",
        )
