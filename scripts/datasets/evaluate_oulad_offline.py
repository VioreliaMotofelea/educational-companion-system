#!/usr/bin/env python3

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import defaultdict
from pathlib import Path
from typing import Dict, List, Set

import requests


ROOT_DIR = Path(__file__).resolve().parents[2]
DEFAULT_PROCESSED_DIR = ROOT_DIR / "datasets" / "oulad" / "processed"


def load_json_array(path: Path) -> List[dict]:
    with path.open("r", encoding="utf-8") as f:
        data = json.load(f)
    if not isinstance(data, list):
        raise ValueError(f"Expected JSON array in {path}")
    return data


def precision_at_k(recommended: List[str], relevant: Set[str], k: int) -> float:
    if k <= 0:
        return 0.0
    top_k = recommended[:k]
    if not top_k:
        return 0.0
    hits = sum(1 for item in top_k if item in relevant)
    return hits / len(top_k)


def recall_at_k(recommended: List[str], relevant: Set[str], k: int) -> float:
    if not relevant:
        return 0.0
    top_k = recommended[:k]
    hits = sum(1 for item in top_k if item in relevant)
    return hits / len(relevant)


def hit_rate_at_k(recommended: List[str], relevant: Set[str], k: int) -> float:
    top_k = recommended[:k]
    return 1.0 if any(item in relevant for item in top_k) else 0.0


def ndcg_at_k(recommended: List[str], relevant: Set[str], k: int) -> float:
    dcg = 0.0
    for i, item in enumerate(recommended[:k]):
        if item in relevant:
            dcg += 1.0 / math.log2(i + 2)

    ideal_hits = min(len(relevant), k)
    if ideal_hits == 0:
        return 0.0

    idcg = sum(1.0 / math.log2(i + 2) for i in range(ideal_hits))
    return dcg / idcg


def pair_dissimilarity(resource_a: dict, resource_b: dict) -> float:
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


def diversity_at_k(recommended: List[str], resource_by_id: Dict[str, dict], k: int) -> float:
    items = recommended[:k]
    if len(items) < 2:
        return 0.0

    scores: List[float] = []
    for i in range(len(items)):
        for j in range(i + 1, len(items)):
            a = resource_by_id.get(items[i])
            b = resource_by_id.get(items[j])
            if not a or not b:
                continue
            scores.append(pair_dissimilarity(a, b))
    return sum(scores) / len(scores) if scores else 0.0


def novelty_at_k(
    recommended: List[str],
    interaction_counts: Dict[str, int],
    total_interactions: int,
    catalog_size: int,
    k: int,
) -> float:
    items = recommended[:k]
    if not items or catalog_size <= 0:
        return 0.0

    denominator = total_interactions + catalog_size
    if denominator <= 1:
        return 0.0

    max_self_information = -math.log2(1.0 / denominator)
    if max_self_information <= 0:
        return 0.0

    novelty_scores: List[float] = []
    for item in items:
        count = interaction_counts.get(item, 0)
        probability = (count + 1) / denominator
        self_information = -math.log2(probability)
        novelty_scores.append(self_information / max_self_information)
    return sum(novelty_scores) / len(novelty_scores)


def extract_recommendation_ids(payload) -> List[str]:
    """
    Adjust this if your backend returns a different response shape.
    Expected common shapes:
      [
        {"learningResourceId": "..."},
        {"resource": {"id": "..."}}
      ]
    """
    result = []

    if isinstance(payload, dict):
        items = payload.get("items") or payload.get("recommendations") or []
    else:
        items = payload

    for item in items:
        if not isinstance(item, dict):
            continue

        rid = (
            item.get("learningResourceId")
            or item.get("resourceId")
            or item.get("id")
        )

        if not rid and isinstance(item.get("resource"), dict):
            rid = item["resource"].get("id")

        if rid:
            result.append(str(rid))

    return result


def aggregate_mean(rows: List[dict], key: str) -> float:
    if not rows:
        return 0.0
    return sum(float(r.get(key, 0.0)) for r in rows) / len(rows)


def evaluate_quality_experiment(rows: List[dict], k: int, label: str) -> dict:
    return {
        "experiment": f"Experiment 1 - {label} quality",
        "evaluatedUsers": len(rows),
        f"precision@{k}": aggregate_mean(rows, f"precision@{k}"),
        f"recall@{k}": aggregate_mean(rows, f"recall@{k}"),
        f"ndcg@{k}": aggregate_mean(rows, f"ndcg@{k}"),
        f"hitRate@{k}": aggregate_mean(rows, f"hitRate@{k}"),
    }


def evaluate_cold_start_experiment(rows: List[dict], k: int, label: str) -> dict:
    buckets = {
        "cold_0_2": [],
        "warm_3_9": [],
        "hot_10_plus": [],
    }
    for r in rows:
        train_count = int(r.get("trainCompletedCount", 0))
        if train_count <= 2:
            buckets["cold_0_2"].append(r)
        elif train_count <= 9:
            buckets["warm_3_9"].append(r)
        else:
            buckets["hot_10_plus"].append(r)

    result = {"experiment": f"Experiment 2 - Cold-start simulation ({label})", "buckets": {}}
    for bucket_name, bucket_rows in buckets.items():
        result["buckets"][bucket_name] = {
            "users": len(bucket_rows),
            f"precision@{k}": aggregate_mean(bucket_rows, f"precision@{k}"),
            f"recall@{k}": aggregate_mean(bucket_rows, f"recall@{k}"),
            f"ndcg@{k}": aggregate_mean(bucket_rows, f"ndcg@{k}"),
            f"hitRate@{k}": aggregate_mean(bucket_rows, f"hitRate@{k}"),
        }
    return result


def evaluate_coverage_diversity_novelty(
    rows: List[dict],
    recommended_lists: Dict[str, List[str]],
    resource_by_id: Dict[str, dict],
    interaction_counts: Dict[str, int],
    total_interactions: int,
    catalog_size: int,
    k: int,
) -> dict:
    unique_recommended: Set[str] = set()
    diversity_scores: List[float] = []
    novelty_scores: List[float] = []

    for user_id, recs in recommended_lists.items():
        top_k = recs[:k]
        unique_recommended.update(top_k)
        diversity_scores.append(diversity_at_k(top_k, resource_by_id, k))
        novelty_scores.append(
            novelty_at_k(
                top_k,
                interaction_counts=interaction_counts,
                total_interactions=total_interactions,
                catalog_size=catalog_size,
                k=k,
            )
        )

    coverage = len(unique_recommended) / catalog_size if catalog_size else 0.0

    return {
        "experiment": "Experiment 3 - Coverage / Diversity / Novelty",
        "evaluatedUsers": len(rows),
        "catalogCoverage": coverage,
        "diversity": (sum(diversity_scores) / len(diversity_scores)) if diversity_scores else 0.0,
        "novelty": (sum(novelty_scores) / len(novelty_scores)) if novelty_scores else 0.0,
    }


def evaluate_rows_from_recommendations(
    user_ids: List[str],
    recommended_lists: Dict[str, List[str]],
    relevant_by_user: Dict[str, Set[str]],
    train_completed_count_by_user: Dict[str, int],
    k: int,
) -> List[dict]:
    rows: List[dict] = []
    for user_id in user_ids:
        recommended = recommended_lists.get(user_id, [])
        relevant = relevant_by_user.get(user_id, set())
        rows.append(
            {
                "userId": user_id,
                "relevantCount": len(relevant),
                "recommendedCount": len(recommended),
                "trainCompletedCount": int(train_completed_count_by_user.get(user_id, 0)),
                f"precision@{k}": precision_at_k(recommended, relevant, k),
                f"recall@{k}": recall_at_k(recommended, relevant, k),
                f"hitRate@{k}": hit_rate_at_k(recommended, relevant, k),
                f"ndcg@{k}": ndcg_at_k(recommended, relevant, k),
            }
        )
    return rows


def build_popularity_recommendations(
    user_ids: List[str],
    completed_counts: Dict[str, int],
    user_completed_train: Dict[str, Set[str]],
    catalog_resource_ids: Set[str],
    k: int,
) -> Dict[str, List[str]]:
    global_ranked = sorted(catalog_resource_ids, key=lambda rid: completed_counts.get(rid, 0), reverse=True)
    recommended_lists: Dict[str, List[str]] = {}
    for user_id in user_ids:
        excluded = user_completed_train.get(user_id, set())
        recommended_lists[user_id] = [rid for rid in global_ranked if rid not in excluded][:k]
    return recommended_lists


def compare_quality_metrics(hybrid: dict, popularity: dict, k: int) -> dict:
    keys = [f"precision@{k}", f"recall@{k}", f"ndcg@{k}", f"hitRate@{k}"]
    return {
        key: float(hybrid.get(key, 0.0)) - float(popularity.get(key, 0.0))
        for key in keys
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Offline evaluation for OULAD recommender.")
    parser.add_argument("--processed-dir", type=Path, default=DEFAULT_PROCESSED_DIR)
    parser.add_argument("--backend-url", default="http://localhost:5235")
    parser.add_argument("--ai-url", default="http://127.0.0.1:8001")
    parser.add_argument("--k", type=int, default=10)
    parser.add_argument("--max-users", type=int, default=0)
    parser.add_argument("--min-test-completed", type=int, default=1)
    parser.add_argument(
        "--sort-users-by-test-size",
        action="store_true",
        help="Evaluate users with most completed test interactions first.",
    )
    parser.add_argument("--timeout-s", type=int, default=30)
    args = parser.parse_args()

    resources = load_json_array(args.processed_dir / "resources.json")
    train = load_json_array(args.processed_dir / "interactions_train.json")
    test = load_json_array(args.processed_dir / "interactions_test.json")
    resource_by_id = {str(r["id"]): r for r in resources if r.get("id") is not None}
    catalog_size = len(resource_by_id)

    # Relevant items for evaluation are Completed interactions from test split.
    relevant_by_user: Dict[str, Set[str]] = defaultdict(set)
    for rec in test:
        if rec.get("interactionType") != "Completed":
            continue
        user_id = str(rec["userId"])
        resource_id = str(rec["learningResourceId"])
        relevant_by_user[user_id].add(resource_id)

    # Train completed count drives the cold-start buckets.
    train_completed_count_by_user: Dict[str, int] = defaultdict(int)
    user_completed_train: Dict[str, Set[str]] = defaultdict(set)
    completed_counts: Dict[str, int] = defaultdict(int)
    interaction_counts: Dict[str, int] = defaultdict(int)
    for rec in train:
        user_id = str(rec.get("userId", ""))
        rid = str(rec.get("learningResourceId", ""))
        if rec.get("interactionType") == "Completed":
            if user_id:
                train_completed_count_by_user[user_id] += 1
                user_completed_train[user_id].add(rid)
            if rid:
                completed_counts[rid] += 1
                interaction_counts[rid] += 1
    total_interactions = len(train)

    users = [u for u, rel in relevant_by_user.items() if len(rel) >= max(1, args.min_test_completed)]
    users = sorted(users)
    if args.sort_users_by_test_size:
        users.sort(key=lambda u: len(relevant_by_user[u]), reverse=True)

    if args.max_users > 0:
        users = users[: args.max_users]

    if not users:
        raise SystemExit("No users with Completed interactions in test set.")

    backend = args.backend_url.rstrip("/")
    ai = args.ai_url.rstrip("/")
    session = requests.Session()

    hybrid_rows = []
    recommended_lists: Dict[str, List[str]] = {}
    for idx, user_id in enumerate(users, start=1):
        print(f"[{idx}/{len(users)}] Evaluating {user_id}")

        gen_resp = session.post(f"{ai}/generate/{user_id}", timeout=args.timeout_s)
        if gen_resp.status_code not in (200, 201):
            print(f"  skip: generate failed {gen_resp.status_code}")
            continue

        rec_resp = session.get(
            f"{backend}/api/users/{user_id}/recommendations?limit={args.k}",
            timeout=args.timeout_s,
        )
        if rec_resp.status_code != 200:
            print(f"  skip: recommendations failed {rec_resp.status_code}")
            continue

        recommended = extract_recommendation_ids(rec_resp.json())
        # Keep evaluation fair across models: exclude items already completed in train.
        excluded = user_completed_train.get(user_id, set())
        recommended = [rid for rid in recommended if rid not in excluded]
        recommended_lists[user_id] = recommended
        relevant = relevant_by_user[user_id]

        row = {
            "userId": user_id,
            "relevantCount": len(relevant),
            "recommendedCount": len(recommended),
            "trainCompletedCount": int(train_completed_count_by_user.get(user_id, 0)),
            f"precision@{args.k}": precision_at_k(recommended, relevant, args.k),
            f"recall@{args.k}": recall_at_k(recommended, relevant, args.k),
            f"hitRate@{args.k}": hit_rate_at_k(recommended, relevant, args.k),
            f"ndcg@{args.k}": ndcg_at_k(recommended, relevant, args.k),
        }
        hybrid_rows.append(row)

    if not hybrid_rows:
        raise SystemExit("No users evaluated successfully.")

    evaluated_user_ids = [r["userId"] for r in hybrid_rows]
    popularity_recommended_lists = build_popularity_recommendations(
        user_ids=evaluated_user_ids,
        completed_counts=completed_counts,
        user_completed_train=user_completed_train,
        catalog_resource_ids=set(resource_by_id.keys()),
        k=args.k,
    )
    popularity_rows = evaluate_rows_from_recommendations(
        user_ids=evaluated_user_ids,
        recommended_lists=popularity_recommended_lists,
        relevant_by_user=relevant_by_user,
        train_completed_count_by_user=train_completed_count_by_user,
        k=args.k,
    )

    hybrid_exp1 = evaluate_quality_experiment(hybrid_rows, args.k, "Hybrid recommender")
    hybrid_exp2 = evaluate_cold_start_experiment(hybrid_rows, args.k, "Hybrid recommender")
    hybrid_exp3 = evaluate_coverage_diversity_novelty(
        rows=hybrid_rows,
        recommended_lists=recommended_lists,
        resource_by_id=resource_by_id,
        interaction_counts=dict(interaction_counts),
        total_interactions=total_interactions,
        catalog_size=catalog_size,
        k=args.k,
    )
    popularity_exp1 = evaluate_quality_experiment(popularity_rows, args.k, "Popularity baseline")
    popularity_exp2 = evaluate_cold_start_experiment(popularity_rows, args.k, "Popularity baseline")
    popularity_exp3 = evaluate_coverage_diversity_novelty(
        rows=popularity_rows,
        recommended_lists=popularity_recommended_lists,
        resource_by_id=resource_by_id,
        interaction_counts=dict(interaction_counts),
        total_interactions=total_interactions,
        catalog_size=catalog_size,
        k=args.k,
    )

    quality_delta = compare_quality_metrics(hybrid_exp1, popularity_exp1, args.k)

    summary = {
        "experimentConfig": {
            "processedDir": str(args.processed_dir),
            "backendUrl": args.backend_url,
            "aiUrl": args.ai_url,
            "k": args.k,
            "minTestCompleted": max(1, args.min_test_completed),
            "maxUsers": args.max_users,
            "sortUsersByTestSize": bool(args.sort_users_by_test_size),
            "timeoutSeconds": args.timeout_s,
        },
        "k": args.k,
        "evaluatedUsers": len(hybrid_rows),
        "usersWithTestCompleted": len(users),
        "catalogSize": catalog_size,
        "experiments": {
            "hybrid_quality": hybrid_exp1,
            "popularity_baseline_quality": popularity_exp1,
            "quality_delta_hybrid_minus_popularity": quality_delta,
            "cold_start_hybrid": hybrid_exp2,
            "cold_start_popularity": popularity_exp2,
            "coverage_diversity_novelty_hybrid": hybrid_exp3,
            "coverage_diversity_novelty_popularity": popularity_exp3,
        },
    }

    out_json = args.processed_dir / "evaluation_report.json"
    out_csv = args.processed_dir / "evaluation_per_user.csv"

    with out_json.open("w", encoding="utf-8") as f:
        json.dump(
            {
                "summary": summary,
                "perUserHybrid": hybrid_rows,
                "perUserPopularity": popularity_rows,
            },
            f,
            indent=2,
        )

    merged_rows = []
    for row in hybrid_rows:
        item = dict(row)
        item["model"] = "hybrid"
        merged_rows.append(item)
    for row in popularity_rows:
        item = dict(row)
        item["model"] = "popularity_baseline"
        merged_rows.append(item)

    csv_fieldnames = [
        "model",
        "userId",
        "relevantCount",
        "recommendedCount",
        "trainCompletedCount",
        f"precision@{args.k}",
        f"recall@{args.k}",
        f"hitRate@{args.k}",
        f"ndcg@{args.k}",
    ]
    with out_csv.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=csv_fieldnames)
        writer.writeheader()
        writer.writerows(merged_rows)

    print("\nEvaluation summary:")
    print(f"  evaluatedUsers: {summary['evaluatedUsers']}")
    print(f"  catalogSize: {summary['catalogSize']}")
    print(f"  hybrid precision@{args.k}: {hybrid_exp1[f'precision@{args.k}']:.4f}")
    print(f"  popularity precision@{args.k}: {popularity_exp1[f'precision@{args.k}']:.4f}")
    print(f"  hybrid recall@{args.k}: {hybrid_exp1[f'recall@{args.k}']:.4f}")
    print(f"  popularity recall@{args.k}: {popularity_exp1[f'recall@{args.k}']:.4f}")
    print(f"  hybrid ndcg@{args.k}: {hybrid_exp1[f'ndcg@{args.k}']:.4f}")
    print(f"  popularity ndcg@{args.k}: {popularity_exp1[f'ndcg@{args.k}']:.4f}")
    print(f"  hybrid hitRate@{args.k}: {hybrid_exp1[f'hitRate@{args.k}']:.4f}")
    print(f"  popularity hitRate@{args.k}: {popularity_exp1[f'hitRate@{args.k}']:.4f}")
    print(f"  hybrid coverage: {hybrid_exp3['catalogCoverage']:.4f}")
    print(f"  popularity coverage: {popularity_exp3['catalogCoverage']:.4f}")
    print(f"  hybrid diversity: {hybrid_exp3['diversity']:.4f}")
    print(f"  popularity diversity: {popularity_exp3['diversity']:.4f}")
    print(f"  hybrid novelty: {hybrid_exp3['novelty']:.4f}")
    print(f"  popularity novelty: {popularity_exp3['novelty']:.4f}")

    print(f"\nSaved: {out_json}")
    print(f"Saved: {out_csv}")


if __name__ == "__main__":
    main()