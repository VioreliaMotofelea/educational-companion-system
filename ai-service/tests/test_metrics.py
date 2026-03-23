import math

from evaluation.metrics import (
    precision_at_k,
    recall_at_k,
    ndcg_at_k,
    ctr,
    completion_rate,
    coverage,
    diversity_at_k,
    novelty_at_k,
)


def test_precision_at_k_basic():
    recommended = ["a", "b", "c"]
    relevant = ["b", "d"]
    assert precision_at_k(recommended, relevant, 2) == 0.5


def test_precision_at_k_k0():
    assert precision_at_k(["a"], ["a"], 0) == 0


def test_recall_at_k_basic():
    recommended = ["a", "b", "c"]
    relevant = ["b", "d"]
    assert recall_at_k(recommended, relevant, 2) == 0.5


def test_recall_at_k_empty_relevant():
    assert recall_at_k(["a", "b"], [], 2) == 0


def _manual_dcg(recommended, relevance_scores, k):
    dcg = 0.0
    for i, item in enumerate(recommended[:k]):
        rel = relevance_scores.get(item, 0)
        dcg += (2 ** rel - 1) / math.log2(i + 2)
    return dcg


def test_ndcg_at_k_known_values():
    recommended = ["x", "b", "y"]
    # b is clicked (1), y is completed (2)
    relevance_scores = {"b": 1, "y": 2}
    k = 3

    actual = _manual_dcg(recommended, relevance_scores, k)
    ideal_rels = sorted(relevance_scores.values(), reverse=True)[:k]
    ideal = 0.0
    for i, rel in enumerate(ideal_rels):
        ideal += (2 ** rel - 1) / math.log2(i + 2)

    expected = 0.0 if ideal == 0 else actual / ideal
    got = ndcg_at_k(recommended, relevance_scores, k)
    assert abs(got - expected) < 1e-12


def test_ctr_and_completion_rate():
    logs = [
        {
            "recommended_items": ["a", "b"],
            "clicked_items": ["b"],
            "completed_items": ["b"],
        },
        {
            "recommended_items": ["c", "d"],
            "clicked_items": [],
            "completed_items": [],
        },
    ]
    assert ctr(logs) == 1 / 4
    assert completion_rate(logs) == 1 / 4


def test_coverage():
    logs = [
        {"recommended_items": ["a", "b"]},
        {"recommended_items": ["b", "c"]},
    ]
    assert coverage(logs, total_items=10) == 3 / 10


def test_diversity_and_novelty_bounds():
    resource_by_id = {
        "a": {"topic": "T1", "contentType": "Article", "difficulty": 1},
        "b": {"topic": "T1", "contentType": "Article", "difficulty": 3},
        "c": {"topic": "T2", "contentType": "Video", "difficulty": 5},
    }
    recommended = ["a", "b", "c"]
    div = diversity_at_k(recommended, resource_by_id, k=3)
    assert 0.0 <= div <= 1.0

    interaction_counts = {"a": 0, "b": 10, "c": 1}
    novelty = novelty_at_k(
        recommended,
        interaction_counts,
        total_interactions=sum(interaction_counts.values()),
        catalog_size=3,
        k=3,
    )
    assert 0.0 <= novelty <= 1.0

