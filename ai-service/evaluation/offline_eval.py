from .evaluator import Evaluator


def _to_item_ids(recommendations):
    item_ids = []
    for rec in recommendations:
        if hasattr(rec, "learningResourceId"):
            item_ids.append(str(rec.learningResourceId))
        elif isinstance(rec, dict) and "learningResourceId" in rec:
            item_ids.append(str(rec["learningResourceId"]))
        else:
            item_ids.append(str(rec))
    return item_ids


def offline_evaluation(model, test_data, k=5):
    """
    model: recommender model (your AI service)
    test_data: dict {user_id: ground_truth_items}
    """

    logs = []

    for user_id, ground_truth in test_data.items():
        recommendations = model.recommend(user_id)
        recommended_items = _to_item_ids(recommendations)
        ground_truth_items = [str(item) for item in ground_truth]

        log = {
            "user_id": user_id,
            "recommended_items": recommended_items,
            "clicked_items": ground_truth_items,  # simulate relevance
            "completed_items": ground_truth_items,
        }

        logs.append(log)

    evaluator = Evaluator(k=k)
    results = evaluator.evaluate(logs)

    print("\n--- OFFLINE EVALUATION ---")
    for key, value in results.items():
        print(f"{key}: {value:.4f}")

    return results