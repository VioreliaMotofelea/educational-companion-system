import math


# ----------------------------
# BASIC METRICS
# ----------------------------

def precision_at_k(recommended, relevant, k):
    recommended_k = recommended[:k]
    relevant_set = set(relevant)

    if k == 0:
        return 0

    hits = sum(1 for item in recommended_k if item in relevant_set)
    return hits / k


def recall_at_k(recommended, relevant, k):
    recommended_k = recommended[:k]
    relevant_set = set(relevant)

    if not relevant_set:
        return 0

    hits = sum(1 for item in recommended_k if item in relevant_set)
    return hits / len(relevant_set)


# ----------------------------
# NDCG (MULTI-LEVEL RELEVANCE)
# ----------------------------

def dcg_at_k(recommended, relevance_scores, k):
    """
    relevance_scores: dict {item_id: relevance_score}
    relevance_score:
        2 = completed
        1 = clicked
        0 = not relevant
    """
    dcg = 0.0

    for i, item in enumerate(recommended[:k]):
        rel = relevance_scores.get(item, 0)
        dcg += (2 ** rel - 1) / math.log2(i + 2)

    return dcg


def ndcg_at_k(recommended, relevance_scores, k):
    actual_dcg = dcg_at_k(recommended, relevance_scores, k)

    # ideal ranking
    sorted_rels = sorted(relevance_scores.values(), reverse=True)
    ideal_list = sorted_rels[:k]

    ideal_dcg = 0.0
    for i, rel in enumerate(ideal_list):
        ideal_dcg += (2 ** rel - 1) / math.log2(i + 2)

    if ideal_dcg == 0:
        return 0

    return actual_dcg / ideal_dcg


# ----------------------------
# BEHAVIORAL METRICS
# ----------------------------

def ctr(logs):
    clicks = sum(len(log.get("clicked_items", [])) for log in logs)
    total = sum(len(log.get("recommended_items", [])) for log in logs)

    return clicks / total if total else 0


def completion_rate(logs):
    completed = sum(len(log.get("completed_items", [])) for log in logs)
    recommended = sum(len(log.get("recommended_items", [])) for log in logs)

    return completed / recommended if recommended else 0


# ----------------------------
# COVERAGE
# ----------------------------

def coverage(logs, total_items):
    recommended_items = set()

    for log in logs:
        recommended_items.update(log.get("recommended_items", []))

    return len(recommended_items) / total_items if total_items else 0