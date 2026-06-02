import numpy as np
import pytest

from recommender import content_semantic as semantic_mod
from recommender.content_semantic import (
    _semantic_explanation_topic,
    build_resource_semantic_text,
    clear_content_semantic_cache,
    generate_content_semantic,
)


class _FakeModel:
    def encode(self, texts, **_kwargs):
        vectors = []
        for text in texts:
            seed = sum(ord(c) for c in text) % 997
            vec = np.array([seed, len(text), hash(text) % 1000], dtype=np.float32)
            vec = vec / np.linalg.norm(vec)
            vectors.append(vec)
        return np.vstack(vectors)


@pytest.fixture(autouse=True)
def _reset_semantic_cache(monkeypatch):
    clear_content_semantic_cache()
    monkeypatch.setattr(semantic_mod, "_model_instances", {})
    yield
    clear_content_semantic_cache()


def test_build_resource_semantic_text_structured_with_optional_fields():
    text = build_resource_semantic_text(
        {
            "title": "Intro Python",
            "topic": "Python",
            "description": "Learn variables and types.",
            "contentType": "Video",
            "week": "Week 2",
            "activityType": "oucontent",
        }
    )
    assert text.splitlines() == [
        "Title: Intro Python",
        "Topic: Python",
        "Description: Learn variables and types.",
        "Type: Video",
        "Week: Week 2",
        "Activity: oucontent",
    ]


def test_build_resource_semantic_text_skips_empty_and_duplicate_values():
    text = build_resource_semantic_text(
        {
            "title": "Intro Python",
            "topic": "Python",
            "description": "Python",
            "contentType": "Video",
            "content": "  ",
        }
    )
    assert text.splitlines() == [
        "Title: Intro Python",
        "Topic: Python",
        "Type: Video",
    ]


def test_build_resource_semantic_text_supports_field_aliases():
    text = build_resource_semantic_text(
        {
            "title": "Quiz 1",
            "topic": "Math",
            "desc": "Short summary",
            "body": "Longer quiz instructions here.",
            "content_type": "Quiz",
            "week_label": "Week 3",
        }
    )
    assert "Description: Short summary" in text
    assert "Content: Longer quiz instructions here." in text
    assert "Type: Quiz" in text
    assert "Week: Week 3" in text


def test_build_resource_semantic_text_handles_missing_fields():
    assert build_resource_semantic_text({}) == "untitled resource"
    assert build_resource_semantic_text({"title": "", "topic": "", "description": ""}) == "untitled resource"


def test_semantic_fingerprint_changes_when_semantic_text_changes():
    from recommender.content_semantic import _resources_semantic_fingerprint

    base = [{"id": "r1", "title": "A", "topic": "T", "description": "d"}]
    with_type = [{**base[0], "contentType": "Video"}]
    assert _resources_semantic_fingerprint(base) != _resources_semantic_fingerprint(with_type)


def test_semantic_explanation_topic_fallback():
    assert _semantic_explanation_topic({"topic": "Python"}) == "Python"
    assert _semantic_explanation_topic({"topic": ""}) == "a related topic"
    assert _semantic_explanation_topic({}) == "a related topic"


def test_generate_content_semantic_returns_empty_without_completed():
    resources = [
        {"id": "r1", "title": "A", "topic": "T", "description": "d1"},
        {"id": "r2", "title": "B", "topic": "T", "description": "d2"},
    ]
    interactions = [{"learningResourceId": "r1", "interactionType": "Viewed"}]
    assert generate_content_semantic({}, interactions, resources, top_k=5) == []


def test_generate_content_semantic_clamps_negative_scores(monkeypatch):
    monkeypatch.setattr(
        semantic_mod,
        "_get_resource_embeddings",
        lambda resources, model_name: np.array(
            [
                [1.0, 0.0],
                [-1.0, 0.0],
                [0.0, 1.0],
            ],
            dtype=np.float32,
        ),
    )

    resources = [
        {"id": "r1", "title": "A", "topic": "T", "description": "d1"},
        {"id": "r2", "title": "B", "topic": "T", "description": "d2"},
        {"id": "r3", "title": "C", "topic": "T", "description": "d3"},
    ]
    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]

    recs = generate_content_semantic({}, interactions, resources, top_k=3, model_name="test-model")
    scores = {r.learningResourceId: r.score for r in recs}
    assert scores["r2"] == 0.0
    assert scores["r3"] >= 0.0


def test_generate_content_semantic_ranks_by_user_mean_embedding(tmp_path, monkeypatch):
    monkeypatch.setattr(semantic_mod, "SEMANTIC_CACHE_DIR", tmp_path)
    monkeypatch.setattr(semantic_mod, "_get_sentence_transformer", lambda _name: _FakeModel())

    resources = [
        {"id": "r1", "title": "Intro Python", "topic": "Python", "description": "basics"},
        {"id": "r2", "title": "Python Data", "topic": "Python", "description": "lists"},
        {"id": "r3", "title": "SQL Basics", "topic": "Databases", "description": "select"},
    ]
    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]

    recs = generate_content_semantic({}, interactions, resources, top_k=3, model_name="test-model")
    assert len(recs) == 2
    assert all(r.algorithmUsed == "ContentBased-Semantic" for r in recs)
    assert all(r.learningResourceId != "r1" for r in recs)
    assert {r.learningResourceId for r in recs} == {"r2", "r3"}
    assert all(0.0 <= r.score <= 1.0 for r in recs)


def test_generate_content_semantic_normalizes_numeric_ids(tmp_path, monkeypatch):
    monkeypatch.setattr(semantic_mod, "SEMANTIC_CACHE_DIR", tmp_path)
    monkeypatch.setattr(semantic_mod, "_get_sentence_transformer", lambda _name: _FakeModel())

    resources = [
        {"id": 1, "title": "A", "topic": "T", "description": "one"},
        {"id": 2, "title": "B", "topic": "T", "description": "two"},
    ]
    interactions = [{"learningResourceId": 1, "interactionType": "Completed"}]

    recs = generate_content_semantic({}, interactions, resources, top_k=2, model_name="test-model")
    assert all(isinstance(r.learningResourceId, str) for r in recs)
    assert {r.learningResourceId for r in recs} == {"2"}


def test_model_cache_is_per_model_name(monkeypatch):
    created: list[str] = []

    class FakeSentenceTransformer:
        def __init__(self, model_name: str):
            created.append(model_name)

    monkeypatch.setattr(
        "sentence_transformers.SentenceTransformer",
        FakeSentenceTransformer,
    )

    semantic_mod._get_sentence_transformer("model-a")
    semantic_mod._get_sentence_transformer("model-a")
    semantic_mod._get_sentence_transformer("model-b")

    assert created == ["model-a", "model-b"]
    assert set(semantic_mod._model_instances) == {"model-a", "model-b"}


def test_semantic_disk_cache_is_reused(tmp_path, monkeypatch):
    monkeypatch.setattr(semantic_mod, "SEMANTIC_CACHE_DIR", tmp_path)
    monkeypatch.setattr(semantic_mod, "_get_sentence_transformer", lambda _name: _FakeModel())

    resources = [
        {"id": "r1", "title": "A", "topic": "T", "description": "one"},
        {"id": "r2", "title": "B", "topic": "T", "description": "two"},
    ]
    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]

    generate_content_semantic({}, interactions, resources, top_k=2, model_name="test-model")
    cache_files = list(tmp_path.rglob("*.npz"))
    assert len(cache_files) == 1

    clear_content_semantic_cache()
    generate_content_semantic({}, interactions, resources, top_k=2, model_name="test-model")
    assert len(list(tmp_path.rglob("*.npz"))) == 1
