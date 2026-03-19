import random

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


def offline_evaluation(
    model,
    test_data,
    k=5,
    click_rate=0.8,
    non_relevant_click_rate=0.05,
    completion_given_click_rate=0.5,
    completion_given_non_relevant_click_rate=0.1,
    seed=42,
):
    """
    model: recommender model (your AI service)
    test_data: dict {user_id: ground_truth_items}
    click_rate: probability a relevant recommendation gets clicked
    non_relevant_click_rate: probability a non-relevant recommendation gets clicked (noise)
    completion_given_click_rate: probability a clicked relevant item gets completed
    completion_given_non_relevant_click_rate: probability a clicked non-relevant item gets completed
    seed: random seed for reproducible offline experiments
    """

    rng = random.Random(seed)
    logs = []

    for user_id, ground_truth in test_data.items():
        recommendations = model.recommend(user_id)
        recommended_items = _to_item_ids(recommendations)
        ground_truth_set = {str(item) for item in ground_truth}

        # Realistic funnel with controlled noise:
        # - relevant items are much more likely to be clicked/completed
        # - non-relevant items can still get occasional clicks/completions
        clicked_items = []
        completed_items = []

        for item in recommended_items:
            is_relevant = item in ground_truth_set
            click_prob = click_rate if is_relevant else non_relevant_click_rate
            if rng.random() < click_prob:
                clicked_items.append(item)

        for item in clicked_items:
            is_relevant = item in ground_truth_set
            completion_prob = (
                completion_given_click_rate
                if is_relevant
                else completion_given_non_relevant_click_rate
            )
            if rng.random() < completion_prob:
                completed_items.append(item)

        log = {
            "user_id": user_id,
            "recommended_items": recommended_items,
            "clicked_items": clicked_items,
            "completed_items": completed_items,
        }

        logs.append(log)

    evaluator = Evaluator(k=k)
    results = evaluator.evaluate(logs)

    print("\n--- OFFLINE EVALUATION ---")
    for key, value in results.items():
        print(f"{key}: {value:.4f}")

    return results