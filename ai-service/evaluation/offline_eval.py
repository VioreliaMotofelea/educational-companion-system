from .evaluator import Evaluator


def offline_evaluation(model, test_data, k=5):
    """
    model: recommender model (your AI service)
    test_data: dict {user_id: ground_truth_items}
    """

    logs = []

    for user_id, ground_truth in test_data.items():
        recommendations = model.recommend(user_id)

        log = {
            "user_id": user_id,
            "recommended_items": recommendations,
            "clicked_items": ground_truth,  # simulate relevance
            "completed_items": ground_truth,
        }

        logs.append(log)

    evaluator = Evaluator(k=k)
    results = evaluator.evaluate(logs)

    print("\n--- OFFLINE EVALUATION ---")
    for key, value in results.items():
        print(f"{key}: {value:.4f}")

    return results