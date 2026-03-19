from .metrics import (
    precision_at_k,
    recall_at_k,
    ndcg_at_k,
    ctr,
    completion_rate,
)


class Evaluator:

    def __init__(self, k=5):
        self.k = k

    def _build_relevance(self, log):
        relevance = {}

        for item in log.get("clicked_items", []):
            relevance[item] = max(relevance.get(item, 0), 1)

        for item in log.get("completed_items", []):
            relevance[item] = 2  # highest relevance

        return relevance

    def evaluate(self, logs):
        precision_scores = []
        recall_scores = []
        ndcg_scores = []

        for log in logs:
            recommended = log.get("recommended_items", [])

            relevance_scores = self._build_relevance(log)
            relevant_items = list(relevance_scores.keys())

            precision_scores.append(
                precision_at_k(recommended, relevant_items, self.k)
            )

            recall_scores.append(
                recall_at_k(recommended, relevant_items, self.k)
            )

            ndcg_scores.append(
                ndcg_at_k(recommended, relevance_scores, self.k)
            )

        results = {
            "precision@k": sum(precision_scores) / len(precision_scores) if precision_scores else 0,
            "recall@k": sum(recall_scores) / len(recall_scores) if recall_scores else 0,
            "ndcg@k": sum(ndcg_scores) / len(ndcg_scores) if ndcg_scores else 0,
            "ctr": ctr(logs),
            "completion_rate": completion_rate(logs),
        }

        return results