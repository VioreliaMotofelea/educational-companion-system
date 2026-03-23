"""
Offline metric dashboard generator for the recommender evaluation module.

It simulates click->completion interactions and calls `Evaluator.evaluate` to compute:
`precision@k`, `recall@k`, `ndcg@k`, `ctr`, `completion_rate`, `coverage`, `diversity`, `novelty`.

Run with `--mode mock` (synthetic data) or `--mode backend` (uses backend seed data).
"""

from __future__ import annotations

import argparse
import csv
import os
import random
import sys
from dataclasses import dataclass
from typing import Dict, Iterable, List, Tuple

from pathlib import Path

# Allow imports like `from evaluation.evaluator import Evaluator` when running from repo root.
AI_SERVICE_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_SERVICE_ROOT))

from evaluation.evaluator import Evaluator
from evaluation.item_ids import to_item_ids


MetricName = str


METRICS_ORDER: List[MetricName] = [
    "precision@k",
    "recall@k",
    "ndcg@k",
    "ctr",
    "completion_rate",
    "coverage",
    "diversity",
    "novelty",
]


@dataclass(frozen=True)
class CatalogItem:
    item_id: str
    topic: str
    difficulty: int
    contentType: str

    def to_dict(self) -> dict:
        return {
            "id": self.item_id,
            "topic": self.topic,
            "difficulty": self.difficulty,
            "contentType": self.contentType,
        }


def generate_mock_catalog(num_items: int, seed: int) -> List[CatalogItem]:
    rng = random.Random(seed)
    topics = ["Python", "C#", "AI", "Databases", "Web", "Software Engineering"]
    content_types = ["Article", "Video", "Quiz"]

    items: List[CatalogItem] = []
    for idx in range(num_items):
        item_id = f"item-{idx:04d}"
        topic = rng.choice(topics)
        difficulty = rng.randint(1, 5)
        contentType = rng.choice(content_types)
        items.append(CatalogItem(item_id=item_id, topic=topic, difficulty=difficulty, contentType=contentType))
    return items


def generate_mock_users_and_ground_truth(
    catalog: List[CatalogItem],
    num_users: int,
    ground_truth_size_range: Tuple[int, int],
    seed: int,
) -> Dict[str, List[str]]:
    rng = random.Random(seed)

    topic_to_items: Dict[str, List[CatalogItem]] = {}
    for item in catalog:
        topic_to_items.setdefault(item.topic, []).append(item)

    users: Dict[str, List[str]] = {}
    for u in range(num_users):
        user_id = f"user-{u+1}"
        preferred_topic = rng.choice(list(topic_to_items.keys()))
        preferred_difficulty = rng.randint(1, 5)

        candidates = [
            it
            for it in topic_to_items[preferred_topic]
            if abs(it.difficulty - preferred_difficulty) <= 1
        ]
        if len(candidates) < 1:
            candidates = topic_to_items[preferred_topic]

        gt_size = rng.randint(ground_truth_size_range[0], ground_truth_size_range[1])
        gt_items = rng.sample([c.item_id for c in candidates], k=min(gt_size, len(candidates)))
        users[user_id] = gt_items

    return users


def generate_popularity_from_mock_catalog(
    catalog: List[CatalogItem],
    seed: int,
) -> Tuple[Dict[str, int], int]:
    rng = random.Random(seed)

    items_ids = [c.item_id for c in catalog]
    alpha = 1.2
    weights = [(1.0 / ((i + 1) ** alpha)) for i in range(len(items_ids))]
    s = sum(weights)
    weights = [w / s for w in weights]

    total_interactions = 4000
    counts: Dict[str, int] = {iid: 0 for iid in items_ids}
    for _ in range(total_interactions):
        iid = rng.choices(items_ids, weights=weights, k=1)[0]
        counts[iid] += 1

    return counts, total_interactions


class SimulatedRankingModel:
    """Synthetic recommender: controls relevance rate in top-N and how early relevant items appear."""

    def __init__(
        self,
        all_items: List[str],
        ground_truth_by_user: Dict[str, List[str]],
        *,
        top_n: int,
        quality: float,
        ordering_skill: float,
        seed: int,
    ):
        self.all_items = all_items
        self.ground_truth_by_user = ground_truth_by_user
        self.top_n = top_n
        self.quality = float(quality)
        self.ordering_skill = float(ordering_skill)
        self.seed = int(seed)

    def recommend(self, user_id: str) -> List[str]:
        rng = random.Random(self.seed + hash(user_id) % (2**31 - 1))
        ground_truth = self.ground_truth_by_user[user_id]
        gt_set = set(ground_truth)

        num_relevant = int(round(self.quality * self.top_n))
        num_relevant = max(0, min(num_relevant, len(ground_truth)))
        num_non_relevant = self.top_n - num_relevant

        relevant_items = rng.sample(ground_truth, k=num_relevant) if num_relevant > 0 else []

        non_relevant_pool = [iid for iid in self.all_items if iid not in gt_set]
        if num_non_relevant > 0:
            non_relevant_items = rng.sample(non_relevant_pool, k=min(num_non_relevant, len(non_relevant_pool)))
        else:
            non_relevant_items = []

        while len(relevant_items) + len(non_relevant_items) < self.top_n:
            candidate = rng.choice(self.all_items)
            if candidate not in gt_set and candidate not in non_relevant_items:
                non_relevant_items.append(candidate)

        recs = relevant_items + non_relevant_items

        if self.ordering_skill >= 1.0:
            return recs[: self.top_n]
        if self.ordering_skill <= 0.0:
            rng.shuffle(recs)
            return recs[: self.top_n]

        scored: List[Tuple[float, str]] = []
        for iid in recs:
            is_rel = 1.0 if iid in gt_set else 0.0
            score = is_rel * self.ordering_skill + rng.random() * (1.0 - self.ordering_skill)
            scored.append((score, iid))
        scored.sort(key=lambda x: x[0], reverse=True)
        return [iid for _, iid in scored[: self.top_n]]


def simulate_click_completion_logs(
    *,
    model: SimulatedRankingModel,
    test_data: Dict[str, List[str]],
    k: int,
    click_rate: float,
    non_relevant_click_rate: float,
    completion_given_click_rate: float,
    completion_given_non_relevant_click_rate: float,
    seed: int,
) -> List[dict]:
    rng = random.Random(seed)
    logs: List[dict] = []

    for user_id, ground_truth in test_data.items():
        recommendations = model.recommend(user_id)
        recommended_items = to_item_ids(recommendations)
        ground_truth_set = {str(item) for item in ground_truth}

        clicked_items: List[str] = []
        completed_items: List[str] = []

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

        logs.append(
            {
                "user_id": user_id,
                "recommended_items": recommended_items,
                "clicked_items": clicked_items,
                "completed_items": completed_items,
            }
        )

    return logs


def evaluate_logs_with_catalog(
    *,
    evaluator: Evaluator,
    logs: List[dict],
    catalog_resources: List[dict],
    interaction_counts: Dict[str, int],
    total_interactions: int,
) -> Dict[str, float]:
    total_items = len(catalog_resources)
    resource_by_id = {str(r.get("id")): r for r in catalog_resources if r.get("id") is not None}

    return evaluator.evaluate(
        logs,
        total_items=total_items,
        resource_by_id=resource_by_id,
        interaction_counts=interaction_counts,
        total_interactions=total_interactions,
    )


def _mean(xs: List[float]) -> float:
    return sum(xs) / len(xs) if xs else 0.0


def run_parameter_sweep(
    *,
    sweep_name: str,
    sweep_values: List[float],
    base_model_kwargs: dict,
    test_data: Dict[str, List[str]],
    catalog_resources: List[dict],
    interaction_counts: Dict[str, int],
    total_interactions: int,
    k: int,
    # funnel defaults:
    click_rate: float,
    non_relevant_click_rate: float,
    completion_given_click_rate: float,
    completion_given_non_relevant_click_rate: float,
    model_quality_name: str | None = None,
    seed_base: int = 1234,
    num_seeds_avg: int = 10,
    ordering_skill: float = 0.8,
) -> List[dict]:
    results: List[dict] = []

    evaluator = Evaluator(k=k)

    for i, val in enumerate(sweep_values):
        model_kwargs = dict(base_model_kwargs)
        if model_quality_name is not None and sweep_name == model_quality_name:
            model_kwargs["quality"] = val
        if sweep_name == "click_rate":
            click_rate = val
        elif sweep_name == "non_relevant_click_rate":
            non_relevant_click_rate = val
        elif sweep_name == "completion_given_click_rate":
            completion_given_click_rate = val

        metric_lists: Dict[str, List[float]] = {m: [] for m in METRICS_ORDER}

        for sidx in range(num_seeds_avg):
            seed = seed_base + i * 1000 + sidx

            model = SimulatedRankingModel(
                all_items=base_model_kwargs["all_items"],
                ground_truth_by_user=test_data,
                top_n=base_model_kwargs["top_n"],
                quality=float(model_kwargs["quality"]),
                ordering_skill=ordering_skill,
                seed=seed,
            )

            logs = simulate_click_completion_logs(
                model=model,
                test_data=test_data,
                k=k,
                click_rate=click_rate,
                non_relevant_click_rate=non_relevant_click_rate,
                completion_given_click_rate=completion_given_click_rate,
                completion_given_non_relevant_click_rate=completion_given_non_relevant_click_rate,
                seed=seed,
            )

            evaluated = evaluate_logs_with_catalog(
                evaluator=evaluator,
                logs=logs,
                catalog_resources=catalog_resources,
                interaction_counts=interaction_counts,
                total_interactions=total_interactions,
            )

            for metric_name in METRICS_ORDER:
                metric_lists[metric_name].append(float(evaluated.get(metric_name, 0.0)))

        row = {sweep_name: val}
        for metric_name in METRICS_ORDER:
            row[metric_name] = _mean(metric_lists[metric_name])

        results.append(row)

    return results


def save_csv(rows: List[dict], path: str) -> None:
    if not rows:
        return
    fieldnames = list(rows[0].keys())
    with open(path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def plot_dashboard(rows: List[dict], sweep_name: str, sweep_values: List[float], out_png: str) -> None:
    import matplotlib.pyplot as plt

    fig, axes = plt.subplots(2, 4, figsize=(22, 10), sharex=True)
    axes = axes.flatten()

    for ax, metric_name in zip(axes, METRICS_ORDER):
        ys = [r[metric_name] for r in rows]
        ax.plot(sweep_values, ys, marker="o", linewidth=2)
        ax.set_title(metric_name)
        ax.grid(True, alpha=0.3)
        ax.set_ylim(0.0, 1.05)

    for ax in axes[:4]:
        ax.set_xlabel("")
    for ax in axes[4:]:
        ax.set_xlabel(sweep_name)

    plt.tight_layout()
    plt.savefig(out_png, dpi=160)
    plt.close(fig)


def fetch_backend_catalog_and_interactions(backend_base_url: str) -> Tuple[List[dict], List[dict]]:
    import requests

    resources = requests.get(f"{backend_base_url}/api/resources", timeout=30).json()
    interactions = requests.get(f"{backend_base_url}/api/interactions", timeout=30).json()
    return resources, interactions


def build_test_data_from_backend(resources: List[dict], interactions: List[dict]) -> Dict[str, List[str]]:
    test_data: Dict[str, List[str]] = {}
    for it in interactions:
        if it.get("interactionType") != "Completed":
            continue
        uid = str(it.get("userId"))
        rid = str(it.get("learningResourceId"))
        test_data.setdefault(uid, []).append(rid)

    for uid in list(test_data.keys()):
        test_data[uid] = sorted(list(set(test_data[uid])))
    return test_data


def build_popularity_from_backend(interactions: List[dict]) -> Tuple[Dict[str, int], int]:
    counts: Dict[str, int] = {}
    total = 0
    for it in interactions:
        rid = it.get("learningResourceId")
        if rid is None:
            continue
        rid = str(rid)
        counts[rid] = counts.get(rid, 0) + 1
        total += 1
    return counts, total


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=["mock", "backend"], default="mock")
    parser.add_argument("--backend-base-url", default=os.environ.get("BACKEND_BASE_URL", "http://localhost:5235"))
    parser.add_argument("--out-dir", default="ai-service/evaluation/plots/offline")
    parser.add_argument("--num-users", type=int, default=30)
    parser.add_argument("--num-items", type=int, default=60)
    parser.add_argument("--top-n", type=int, default=10)
    parser.add_argument("--k", type=int, default=5)
    parser.add_argument("--model-quality", type=float, default=0.75)
    parser.add_argument("--ordering-skill", type=float, default=0.8)
    parser.add_argument("--avg-seeds", type=int, default=10)
    args = parser.parse_args()

    os.makedirs(args.out_dir, exist_ok=True)

    def build_mock_inputs():
        catalog_items = generate_mock_catalog(num_items=args.num_items, seed=42)
        catalog_resources = [c.to_dict() for c in catalog_items]
        td = generate_mock_users_and_ground_truth(
            catalog=catalog_items,
            num_users=args.num_users,
            ground_truth_size_range=(5, 10),
            seed=1337,
        )
        ic, total_ic = generate_popularity_from_mock_catalog(catalog_items, seed=7)
        all_it = [c.item_id for c in catalog_items]
        return td, ic, total_ic, all_it, catalog_resources

    if args.mode == "backend":
        try:
            resources, interactions = fetch_backend_catalog_and_interactions(args.backend_base_url)
            test_data = build_test_data_from_backend(resources, interactions)
            interaction_counts, total_interactions = build_popularity_from_backend(interactions)
            all_items = [str(r.get("id")) for r in resources if r.get("id") is not None]

            test_data = {u: gt for u, gt in test_data.items() if len(gt) > 0}

            if not test_data:
                print(
                    "Warning: backend mode produced 0 users with completed items; falling back to mock data."
                )
                test_data, interaction_counts, total_interactions, all_items, resources = build_mock_inputs()
        except Exception as e:
            print(f"Warning: backend mode failed ({e}); falling back to mock data.")
            test_data, interaction_counts, total_interactions, all_items, resources = build_mock_inputs()
    else:
        test_data, interaction_counts, total_interactions, all_items, resources = build_mock_inputs()

    base_model_kwargs = {
        "all_items": all_items,
        "top_n": args.top_n,
        "quality": args.model_quality,
    }

    click_rate_base = 0.6
    non_relevant_click_rate_base = 0.05
    completion_given_click_rate_base = 0.5
    completion_given_non_relevant_click_rate_base = 0.1

    click_rate_values = [round(x, 2) for x in [0.1, 0.2, 0.4, 0.6, 0.8, 0.9]]
    rows_click = run_parameter_sweep(
        sweep_name="click_rate",
        sweep_values=click_rate_values,
        base_model_kwargs=base_model_kwargs,
        test_data=test_data,
        catalog_resources=resources,
        interaction_counts=interaction_counts,
        total_interactions=total_interactions,
        k=args.k,
        click_rate=click_rate_base,
        non_relevant_click_rate=non_relevant_click_rate_base,
        completion_given_click_rate=completion_given_click_rate_base,
        completion_given_non_relevant_click_rate=completion_given_non_relevant_click_rate_base,
        seed_base=111,
        num_seeds_avg=args.avg_seeds,
        ordering_skill=args.ordering_skill,
        model_quality_name=None,
    )

    save_csv(rows_click, os.path.join(args.out_dir, "offline_metrics_sweep_click_rate.csv"))
    plot_dashboard(
        rows_click,
        sweep_name="click_rate",
        sweep_values=click_rate_values,
        out_png=os.path.join(args.out_dir, "offline_metrics_sweep_click_rate.png"),
    )

    completion_values = [round(x, 2) for x in [0.1, 0.2, 0.4, 0.6, 0.8, 0.95]]
    rows_completion = run_parameter_sweep(
        sweep_name="completion_given_click_rate",
        sweep_values=completion_values,
        base_model_kwargs=base_model_kwargs,
        test_data=test_data,
        catalog_resources=resources,
        interaction_counts=interaction_counts,
        total_interactions=total_interactions,
        k=args.k,
        click_rate=click_rate_base,
        non_relevant_click_rate=non_relevant_click_rate_base,
        completion_given_click_rate=completion_given_click_rate_base,
        completion_given_non_relevant_click_rate=completion_given_non_relevant_click_rate_base,
        seed_base=222,
        num_seeds_avg=args.avg_seeds,
        ordering_skill=args.ordering_skill,
        model_quality_name=None,
    )
    save_csv(rows_completion, os.path.join(args.out_dir, "offline_metrics_sweep_completion_given_click.csv"))
    plot_dashboard(
        rows_completion,
        sweep_name="completion_given_click_rate",
        sweep_values=completion_values,
        out_png=os.path.join(args.out_dir, "offline_metrics_sweep_completion_given_click.png"),
    )

    qualities = [0.15, 0.3, 0.5, 0.65, 0.8, 0.95]
    base_model_kwargs_quality = dict(base_model_kwargs)
    rows_quality = run_parameter_sweep(
        sweep_name="model_quality",
        sweep_values=qualities,
        base_model_kwargs=base_model_kwargs_quality,
        test_data=test_data,
        catalog_resources=resources,
        interaction_counts=interaction_counts,
        total_interactions=total_interactions,
        k=args.k,
        click_rate=click_rate_base,
        non_relevant_click_rate=non_relevant_click_rate_base,
        completion_given_click_rate=completion_given_click_rate_base,
        completion_given_non_relevant_click_rate=completion_given_non_relevant_click_rate_base,
        seed_base=333,
        num_seeds_avg=args.avg_seeds,
        ordering_skill=args.ordering_skill,
        model_quality_name="model_quality",
    )
    save_csv(rows_quality, os.path.join(args.out_dir, "offline_metrics_sweep_model_quality.csv"))
    plot_dashboard(
        rows_quality,
        sweep_name="model_quality",
        sweep_values=qualities,
        out_png=os.path.join(args.out_dir, "offline_metrics_sweep_model_quality.png"),
    )

    print(f"Saved plots to: {args.out_dir}")
    print("Generated files:")
    for fn in [
        "offline_metrics_sweep_click_rate.png",
        "offline_metrics_sweep_completion_given_click.png",
        "offline_metrics_sweep_model_quality.png",
    ]:
        print(" -", fn)


if __name__ == "__main__":
    main()

