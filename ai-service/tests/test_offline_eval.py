from evaluation.offline_eval import offline_evaluation


class DummyModel:
    def recommend(self, user_id: str):
        return ["a", "b", "c", "d", "e"] # Stable recommended list for testing


def test_offline_evaluation_runs_and_returns_expected_keys():
    test_data = {
        "u1": ["b", "x"],
        "u2": ["a"],
    }

    results = offline_evaluation(
        model=DummyModel(),
        test_data=test_data,
        k=5,
        seed=123,
    )

    for key in ["precision@k", "recall@k", "ndcg@k", "ctr", "completion_rate"]:
        assert key in results
        assert 0.0 <= results[key] <= 1.0

