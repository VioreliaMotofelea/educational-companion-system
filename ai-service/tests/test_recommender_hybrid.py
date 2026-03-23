from models.recommendation_models import RecommendationItem
from recommender.hybrid import generate_hybrid


def test_hybrid_scoring_and_ranking_with_difficulty(monkeypatch):
    import recommender.hybrid as hybrid_mod

    resources = [
        {"id": "r1", "difficulty": 1},
        {"id": "r2", "difficulty": 1},  # should match difficulty (suggested=1)
        {"id": "r3", "difficulty": 5},  # far away difficulty, difficulty_match=0
    ]

    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]

    def fake_content_based(user, interactions, resources, top_k):
        return [
            RecommendationItem(
                learningResourceId="r2",
                score=0.2,
                algorithmUsed="ContentBased-TFIDF",
                explanation="x",
            ),
            RecommendationItem(
                learningResourceId="r3",
                score=1.0,
                algorithmUsed="ContentBased-TFIDF",
                explanation="y",
            ),
        ]

    def fake_collab(user_id, all_users_interactions, resources, top_k):
        return [
            RecommendationItem(
                learningResourceId="r2",
                score=0.0,
                algorithmUsed="Collaborative-Cosine-KNN",
                explanation="x",
            ),
            RecommendationItem(
                learningResourceId="r3",
                score=0.5,
                algorithmUsed="Collaborative-Cosine-KNN",
                explanation="y",
            ),
        ]

    monkeypatch.setattr(hybrid_mod, "generate_content_based", fake_content_based)
    monkeypatch.setattr(hybrid_mod, "generate_collaborative", fake_collab)

    recs = generate_hybrid(
        user={"userId": "u1", "preferences": {"preferredDifficulty": 3}},
        interactions=interactions,
        all_users_interactions=[],
        resources=resources,
        mastery={"suggestedDifficulty": 1},
        top_k=2,
    )

    assert [r.learningResourceId for r in recs] == ["r3", "r2"]

    # Expected final scores (formula in hybrid.py):
    # r2: 0.5*(0.2/1.0) + 0.3*(0/0.5) + 0.2*1 = 0.3
    # r3: 0.5*(1.0/1.0) + 0.3*(0.5/0.5) + 0.2*0 = 0.8
    assert recs[0].score == 0.8
    assert recs[1].score == 0.3
    assert recs[0].algorithmUsed == "Hybrid"
    assert "suggested level 1" in recs[0].explanation
    assert 0.0 <= recs[0].score <= 1.0


def test_hybrid_fallback_to_user_preferences_when_no_mastery(monkeypatch):
    import recommender.hybrid as hybrid_mod

    resources = [
        {"id": "r1", "difficulty": 1},
        {"id": "r2", "difficulty": 2},  # matches preferredDifficulty=2
        {"id": "r3", "difficulty": 5},
    ]
    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]

    monkeypatch.setattr(hybrid_mod, "generate_content_based", lambda *args, **kwargs: [])
    monkeypatch.setattr(hybrid_mod, "generate_collaborative", lambda *args, **kwargs: [])

    recs = generate_hybrid(
        user={"userId": "u1", "preferences": {"preferredDifficulty": 2}},
        interactions=interactions,
        all_users_interactions=[],
        resources=resources,
        mastery=None,
        top_k=2,
    )

    # With content_s=0 and collab_s=0, final score is 0.2*difficulty_match
    # r2: difficulty_match=1 (diff_gap=0) => score=0.2
    # r3: difficulty_match=0.25 (diff_gap=3 => 1 - 3/4) => score=0.05
    assert [r.learningResourceId for r in recs] == ["r2", "r3"]
    assert recs[0].score == 0.2
    assert recs[1].score == 0.05

