"""Targeted tests for recommender hardening (IDs, binary matrix, score precision)."""

import numpy as np

from recommender.collaborative import (
    clear_collaborative_matrix_cache,
    collaborative_score_map_from_prepared,
    prepare_collaborative_matrix,
)
from recommender.content_based import compute_tfidf_score_map
from recommender.content_semantic import compute_semantic_score_map


def setup_function():
    clear_collaborative_matrix_cache()


def test_collaborative_normalizes_mixed_int_string_ids():
    interactions = [
        {"userId": 42, "learningResourceId": 100, "interactionType": "Completed"},
        {"userId": "42", "learningResourceId": "100", "interactionType": "Completed"},
        {"userId": "u2", "learningResourceId": "200", "interactionType": "Completed"},
    ]
    resources = [{"id": 100}, {"id": "200"}]

    matrix, all_vecs = prepare_collaborative_matrix(interactions, resources)
    assert "42" in matrix.index
    assert "100" in matrix.columns
    assert matrix.loc["42", "100"] == 1.0

    scores = collaborative_score_map_from_prepared(
        42, matrix, all_vecs, top_k=5, neighbors_k=5
    )
    assert all(isinstance(rid, str) for rid in scores)


def test_repeated_completed_interactions_produce_binary_matrix():
    interactions = [
        {"userId": "u1", "learningResourceId": "r1", "interactionType": "Completed"},
        {"userId": "u1", "learningResourceId": "r1", "interactionType": "Completed"},
        {"userId": "u1", "learningResourceId": "r1", "interactionType": "Completed"},
    ]
    resources = [{"id": "r1"}, {"id": "r2"}]

    matrix, _ = prepare_collaborative_matrix(interactions, resources)
    assert float(matrix.loc["u1", "r1"]) == 1.0
    assert float(matrix.loc["u1", "r2"]) == 0.0


def test_nonpositive_neighbor_similarity_returns_empty_map():
    interactions = [
        {"userId": "u1", "learningResourceId": "r1", "interactionType": "Completed"},
        {"userId": "u2", "learningResourceId": "r2", "interactionType": "Completed"},
    ]
    resources = [{"id": "r1"}, {"id": "r2"}]

    matrix, all_vecs = prepare_collaborative_matrix(interactions, resources)
    scores = collaborative_score_map_from_prepared(
        "u1", matrix, all_vecs, top_k=5, neighbors_k=5
    )
    assert scores == {}


def test_collaborative_cache_fingerprint_stable_across_list_copies():
    base = [
        {"userId": "u1", "learningResourceId": "r1", "interactionType": "Completed"},
    ]
    copy = list(base)
    resources = [{"id": "r1"}, {"id": "r2"}]

    m1, _ = prepare_collaborative_matrix(base, resources)
    m2, _ = prepare_collaborative_matrix(copy, resources)
    assert m1 is m2


def test_hybrid_live_path_uses_full_precision_score_maps(monkeypatch):
    import inspect

    import recommender.hybrid as hybrid_mod

    hybrid_source = inspect.getsource(hybrid_mod._build_content_score_map)
    collab_source = inspect.getsource(hybrid_mod.build_collaborative_score_map)
    assert "generate_content_based" not in hybrid_source
    assert "generate_content_semantic" not in hybrid_source
    assert "generate_collaborative" not in collab_source
    assert "compute_tfidf_score_map" in hybrid_source
    assert "compute_semantic_score_map" in hybrid_source
    assert "collaborative_score_map_from_prepared" in collab_source
    assert "prepare_collaborative_matrix" in collab_source


def test_build_content_score_map_preserves_raw_tfidf_values(monkeypatch):
    import recommender.hybrid as hybrid_mod

    raw = {"r1": 0.123456789, "r2": 0.987654321}
    monkeypatch.setattr(hybrid_mod, "compute_tfidf_score_map", lambda i, r: dict(raw))
    monkeypatch.setattr(hybrid_mod, "SEMANTIC_CONTENT_ENABLED", False)

    resources = [{"id": "r1"}, {"id": "r2"}]
    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]
    score_map, mode = hybrid_mod._build_content_score_map({}, interactions, resources)

    assert mode == "tfidf"
    assert score_map == raw


def test_build_collaborative_score_map_without_prepared_skips_generate_collaborative(monkeypatch):
    import pandas as pd

    import recommender.hybrid as hybrid_mod

    called = {"generate_collaborative": False}

    def fail_generate(*args, **kwargs):
        called["generate_collaborative"] = True
        raise AssertionError("generate_collaborative must not be used")

    matrix = pd.DataFrame([[1.0, 0.0], [0.0, 1.0]], index=["u1", "u2"], columns=["r1", "r2"])
    vecs = matrix.to_numpy(dtype=float)

    monkeypatch.setattr("recommender.collaborative.generate_collaborative", fail_generate)
    monkeypatch.setattr(
        hybrid_mod,
        "prepare_collaborative_matrix",
        lambda *args, **kwargs: (matrix, vecs),
    )
    monkeypatch.setattr(
        hybrid_mod,
        "collaborative_score_map_from_prepared",
        lambda user_id, m, v, top_k: {"r2": 0.555555555},
    )

    scores = hybrid_mod.build_collaborative_score_map("u1", [], [{"id": "r1"}, {"id": "r2"}])
    assert scores == {"r2": 0.555555555}
    assert called["generate_collaborative"] is False


def test_build_content_score_map_matches_offline_fusion_helper(monkeypatch):
    import recommender.hybrid as hybrid_mod

    tfidf = {"r1": 0.8, "r2": 0.4}
    semantic = {"r1": 0.2, "r2": 0.6}
    monkeypatch.setattr(hybrid_mod, "compute_tfidf_score_map", lambda i, r: dict(tfidf))
    monkeypatch.setattr(hybrid_mod, "compute_semantic_score_map", lambda i, r, **k: dict(semantic))
    monkeypatch.setattr(hybrid_mod, "SEMANTIC_CONTENT_ENABLED", True)
    monkeypatch.setattr(hybrid_mod, "CONTENT_FUSION_MODE", "tfidf_semantic")
    monkeypatch.setattr(hybrid_mod, "CONTENT_TFIDF_SUBWEIGHT", 0.5)
    monkeypatch.setattr(hybrid_mod, "CONTENT_SEMANTIC_SUBWEIGHT", 0.5)

    resources = [{"id": "r1"}, {"id": "r2"}]
    interactions = [{"learningResourceId": "r1", "interactionType": "Completed"}]

    live_map, live_mode = hybrid_mod._build_content_score_map({}, interactions, resources)
    offline_maps = hybrid_mod.build_all_fusion_content_maps(
        {},
        interactions,
        resources,
        semantic_model_name="test-model",
        tfidf_subweight=0.5,
        semantic_subweight=0.5,
    )
    offline_map, offline_mode = offline_maps["tfidf_semantic"]

    assert live_mode == offline_mode == "tfidf_semantic"
    assert live_map == offline_map


def test_internal_score_maps_do_not_round_before_fusion():
    import inspect

    for fn in (compute_tfidf_score_map, compute_semantic_score_map):
        source = inspect.getsource(fn)
        assert "round(" not in source

    collab_source = inspect.getsource(collaborative_score_map_from_prepared)
    assert "round(" not in collab_source


def test_semantic_score_map_returns_catalog_candidates(monkeypatch):
    resources = [
        {"id": "a", "title": "Course A", "topic": "AAA", "description": "week 1 material"},
        {"id": "b", "title": "Course B", "topic": "AAA", "description": "week 2 material"},
    ]
    interactions = [
        {"userId": "u1", "learningResourceId": "a", "interactionType": "Completed"},
    ]

    emb = np.array([[1.0, 0.0], [0.8, 0.6]], dtype=np.float32)
    monkeypatch.setattr(
        "recommender.content_semantic._get_resource_embeddings",
        lambda _resources, _model: emb,
    )

    score_map = compute_semantic_score_map(interactions, resources, model_name="test-model")
    assert set(score_map.keys()) == {"a", "b"}
    assert score_map["a"] >= score_map["b"]
    assert all(isinstance(v, float) for v in score_map.values())
