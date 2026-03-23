from evaluation.evaluator import Evaluator
from evaluation.metrics import precision_at_k, recall_at_k


def test_evaluator_produces_expected_keys_and_ranges():
    logs = [
        {
            "recommended_items": ["a", "b", "c", "d", "e"],
            "clicked_items": ["b"],
            "completed_items": ["e"],
        }
    ]

    resource_by_id = {
        "a": {"topic": "T1", "contentType": "Article", "difficulty": 1},
        "b": {"topic": "T1", "contentType": "Article", "difficulty": 2},
        "c": {"topic": "T1", "contentType": "Video", "difficulty": 3},
        "d": {"topic": "T2", "contentType": "Video", "difficulty": 4},
        "e": {"topic": "T3", "contentType": "Article", "difficulty": 5},
    }

    interaction_counts = {"a": 0, "b": 5, "c": 1, "d": 2, "e": 10}
    total_interactions = sum(interaction_counts.values())

    ev = Evaluator(k=3)
    results = ev.evaluate(
        logs,
        total_items=10,
        resource_by_id=resource_by_id,
        interaction_counts=interaction_counts,
        total_interactions=total_interactions,
    )

    expected_precision = precision_at_k(["a", "b", "c"], ["b", "e"], 3)
    expected_recall = recall_at_k(["a", "b", "c"], ["b", "e"], 3)

    assert "precision@k" in results
    assert "recall@k" in results
    assert "ndcg@k" in results
    assert "ctr" in results
    assert "completion_rate" in results
    assert "coverage" in results
    assert "diversity" in results
    assert "novelty" in results

    assert abs(results["precision@k"] - expected_precision) < 1e-12
    assert abs(results["recall@k"] - expected_recall) < 1e-12

    for key, value in results.items():
        assert value >= 0.0
        assert value <= 1.0 + 1e-9

