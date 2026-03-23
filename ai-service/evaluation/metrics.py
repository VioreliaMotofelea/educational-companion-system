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


# ----------------------------
# DIVERSITY / NOVELTY
# ----------------------------

def _pair_dissimilarity(resource_a, resource_b):
    topic_a = resource_a.get("topic")
    topic_b = resource_b.get("topic")
    content_a = resource_a.get("contentType")
    content_b = resource_b.get("contentType")
    diff_a = int(resource_a.get("difficulty", 1))
    diff_b = int(resource_b.get("difficulty", 1))

    topic_component = 1.0 if topic_a != topic_b else 0.0
    content_component = 1.0 if content_a != content_b else 0.0
    difficulty_component = min(4, abs(diff_a - diff_b)) / 4.0

    return (topic_component + content_component + difficulty_component) / 3.0


def diversity_at_k(recommended, resource_by_id, k):
    items = recommended[:k]
    if len(items) < 2:
        return 0.0

    dissimilarities = []
    for i in range(len(items)):
        for j in range(i + 1, len(items)):
            a = resource_by_id.get(items[i])
            b = resource_by_id.get(items[j])
            if not a or not b:
                continue
            dissimilarities.append(_pair_dissimilarity(a, b))

    if not dissimilarities:
        return 0.0
    return sum(dissimilarities) / len(dissimilarities)


def novelty_at_k(recommended, interaction_counts, total_interactions, catalog_size, k):
    items = recommended[:k]
    if not items or catalog_size <= 0:
        return 0.0

    denominator = total_interactions + catalog_size
    if denominator <= 1:
        return 0.0

    max_self_information = -math.log2(1.0 / denominator)
    if max_self_information <= 0:
        return 0.0

    novelty_scores = []
    for item in items:
        count = interaction_counts.get(item, 0)
        probability = (count + 1) / denominator  # Laplace smoothing
        self_information = -math.log2(probability)
        novelty_scores.append(self_information / max_self_information)

    return sum(novelty_scores) / len(novelty_scores)