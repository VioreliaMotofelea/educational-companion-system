from recommender.collaborative import generate_collaborative


def test_collaborative_excludes_completed_and_ranks_by_similarity():
    resources = [
        {"id": "r1"},
        {"id": "r2"},
        {"id": "r3"},
    ]
    all_users_interactions = [
        {"userId": "u1", "learningResourceId": "r1", "interactionType": "Completed"},
        {"userId": "u2", "learningResourceId": "r1", "interactionType": "Completed"},
        {"userId": "u2", "learningResourceId": "r2", "interactionType": "Completed"},
        {"userId": "u3", "learningResourceId": "r3", "interactionType": "Completed"},
    ]

    recs = generate_collaborative(
        user_id="u1",
        all_users_interactions=all_users_interactions,
        resources=resources,
        top_k=2,
        neighbors_k=2,
    )

    assert all(r.learningResourceId != "r1" for r in recs)
    assert len(recs) <= 2
    assert recs[0].algorithmUsed == "Collaborative-Cosine-KNN"
    assert recs[0].learningResourceId == "r2"


def test_collaborative_returns_empty_without_user_interactions():
    resources = [{"id": "r1"}, {"id": "r2"}]
    all_users_interactions = [
        {"userId": "u2", "learningResourceId": "r1", "interactionType": "Completed"},
    ]

    recs = generate_collaborative(
        user_id="u1",
        all_users_interactions=all_users_interactions,
        resources=resources,
        top_k=5,
        neighbors_k=2,
    )
    assert recs == []

