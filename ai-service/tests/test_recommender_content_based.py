from recommender.content_based import generate_content_based


def test_content_based_returns_empty_when_no_interactions():
    resources = [
        {"id": "r1", "title": "T1", "topic": "Python", "description": "d1"},
        {"id": "r2", "title": "T2", "topic": "C#", "description": "d2"},
    ]
    assert generate_content_based(
        user={"userId": "u1"},
        interactions=[],
        resources=resources,
        top_k=2,
    ) == []


def test_content_based_returns_empty_when_no_completed_items():
    resources = [
        {"id": "r1", "title": "T1", "topic": "Python", "description": "d1"},
        {"id": "r2", "title": "T2", "topic": "C#", "description": "d2"},
    ]
    interactions = [{"learningResourceId": "r1", "interactionType": "Viewed"}]
    assert generate_content_based(
        user={"userId": "u1"},
        interactions=interactions,
        resources=resources,
        top_k=2,
    ) == []


def test_content_based_excludes_completed_and_limits_top_k():
    resources = [
        {"id": "r1", "title": "Intro Python", "topic": "Python", "description": "basics"},
        {"id": "r2", "title": "Python Data Structures", "topic": "Python", "description": "lists dicts"},
        {"id": "r3", "title": "SQL Basics", "topic": "Databases", "description": "select join"},
    ]
    interactions = [
        {"learningResourceId": "r1", "interactionType": "Completed"},
    ]

    recs = generate_content_based(
        user={"userId": "u1"},
        interactions=interactions,
        resources=resources,
        top_k=2,
    )
    # r1 is already completed, so it must not appear in recommendations
    assert all(r.learningResourceId != "r1" for r in recs)
    assert len(recs) <= 2
    for r in recs:
        assert r.algorithmUsed == "ContentBased-TFIDF"
        assert 0.0 <= r.score <= 1.0
        assert "Similar to resources you completed" in r.explanation

