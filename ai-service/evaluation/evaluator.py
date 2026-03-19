from .metrics import (
    precision_at_k,
    recall_at_k,
    ndcg_at_k,
    ctr,
    completion_rate,
    coverage,
    diversity_at_k,
    novelty_at_k,
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

    def evaluate(
        self,
        logs,
        total_items=None,
        resource_by_id=None,
        interaction_counts=None,
        total_interactions=None,
    ):
        precision_scores = []
        recall_scores = []
        ndcg_scores = []
        diversity_scores = []
        novelty_scores = []

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

            if resource_by_id is not None:
                diversity_scores.append(
                    diversity_at_k(recommended, resource_by_id, self.k)
                )

            if interaction_counts is not None and total_interactions is not None:
                novelty_scores.append(
                    novelty_at_k(
                        recommended,
                        interaction_counts,
                        total_interactions,
                        len(resource_by_id or {}),
                        self.k,
                    )
                )

        results = {
            "precision@k": sum(precision_scores) / len(precision_scores) if precision_scores else 0,
            "recall@k": sum(recall_scores) / len(recall_scores) if recall_scores else 0,
            "ndcg@k": sum(ndcg_scores) / len(ndcg_scores) if ndcg_scores else 0,
            "ctr": ctr(logs),
            "completion_rate": completion_rate(logs),
        }
        if total_items is not None:
            results["coverage"] = coverage(logs, total_items)
        if diversity_scores:
            results["diversity"] = sum(diversity_scores) / len(diversity_scores)
        if novelty_scores:
            results["novelty"] = sum(novelty_scores) / len(novelty_scores)

        return results