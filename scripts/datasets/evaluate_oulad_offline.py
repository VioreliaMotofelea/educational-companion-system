#!/usr/bin/env python3

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import defaultdict
from pathlib import Path
from typing import Dict, List, Sequence, Set

import requests


ROOT_DIR = Path(__file__).resolve().parents[2]
DEFAULT_PROCESSED_DIR = ROOT_DIR / "datasets" / "oulad" / "processed"

VALID_VARIANTS: tuple[str, ...] = (
    "popularity_baseline",
    "hybrid_no_difficulty",
    "hybrid_full",
)

DEFAULT_VARIANTS: List[str] = list(VALID_VARIANTS)


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
    result: List[str] = []

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
        "experiment": f"Experiment 1 - Quality ({label})",
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

    result: dict = {"experiment": f"Experiment 2 - Cold-start ({label})", "buckets": {}}
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
    label: str,
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
        "experiment": f"Experiment 3 - Coverage / Diversity / Novelty ({label})",
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
    global_ranked = sorted(
        catalog_resource_ids,
        key=lambda rid: completed_counts.get(rid, 0),
        reverse=True,
    )
    recommended_lists: Dict[str, List[str]] = {}
    for user_id in user_ids:
        excluded = user_completed_train.get(user_id, set())
        recommended_lists[user_id] = [rid for rid in global_ranked if rid not in excluded][:k]
    return recommended_lists


def merge_quality_and_distribution(quality: dict, coverage_block: dict, k: int) -> dict:
    """Single flat dict for delta comparisons."""
    keys = [f"precision@{k}", f"recall@{k}", f"ndcg@{k}", f"hitRate@{k}"]
    out = {key: float(quality.get(key, 0.0)) for key in keys}
    out["catalogCoverage"] = float(coverage_block.get("catalogCoverage", 0.0))
    out["diversity"] = float(coverage_block.get("diversity", 0.0))
    out["novelty"] = float(coverage_block.get("novelty", 0.0))
    out["evaluatedUsers"] = int(quality.get("evaluatedUsers", 0))
    return out


def subtract_metrics(high: dict, low: dict, k: int) -> dict:
    keys = [f"precision@{k}", f"recall@{k}", f"ndcg@{k}", f"hitRate@{k}"]
    out = {key: float(high.get(key, 0.0)) - float(low.get(key, 0.0)) for key in keys}
    out["catalogCoverage"] = float(high.get("catalogCoverage", 0.0)) - float(low.get("catalogCoverage", 0.0))
    out["diversity"] = float(high.get("diversity", 0.0)) - float(low.get("diversity", 0.0))
    out["novelty"] = float(high.get("novelty", 0.0)) - float(low.get("novelty", 0.0))
    return out


def filter_recommendations_exclude_train(
    recommended: List[str],
    user_id: str,
    user_completed_train: Dict[str, Set[str]],
) -> List[str]:
    excluded = user_completed_train.get(user_id, set())
    return [rid for rid in recommended if rid not in excluded]


def fetch_hybrid_recommendations(
    session: requests.Session,
    backend: str,
    ai: str,
    user_id: str,
    k: int,
    variant: str,
    timeout_s: int,
    user_completed_train: Dict[str, Set[str]],
) -> List[str] | None:
    """POST generate with variant, GET recommendations, exclude train-completed. Returns None on failure."""
    gen_url = f"{ai}/generate/{user_id}"
    gen_resp = session.post(gen_url, params={"variant": variant}, timeout=timeout_s)
    if gen_resp.status_code not in (200, 201):
        return None

    rec_resp = session.get(
        f"{backend}/api/users/{user_id}/recommendations?limit={k}",
        timeout=timeout_s,
    )
    if rec_resp.status_code != 200:
        return None

    recommended = extract_recommendation_ids(rec_resp.json())
    return filter_recommendations_exclude_train(recommended, user_id, user_completed_train)


def resolve_output_dir(processed_dir: Path, output_prefix: str | None) -> Path:
    if not output_prefix or not str(output_prefix).strip():
        return processed_dir
    p = Path(output_prefix).expanduser()
    p.mkdir(parents=True, exist_ok=True)
    return p


def parse_variants_arg(raw: Sequence[str] | None) -> List[str]:
    if raw is None or len(raw) == 0:
        return list(DEFAULT_VARIANTS)
    seen: Set[str] = set()
    ordered: List[str] = []
    for v in raw:
        if v not in VALID_VARIANTS:
            raise SystemExit(f"Invalid variant {v!r}. Choose from: {', '.join(VALID_VARIANTS)}")
        if v not in seen:
            seen.add(v)
            ordered.append(v)
    return ordered


def print_summary_table(
    models_payload: Dict[str, dict],
    k: int,
    variants_order: List[str],
) -> None:
    """Print mean quality + distribution metrics per model."""
    pk, rk, nk, hk = f"precision@{k}", f"recall@{k}", f"ndcg@{k}", f"hitRate@{k}"
    headers = ["model", "users", pk, rk, nk, hk, "coverage", "diversity", "novelty"]
    rows: List[List[str]] = []
    for model in variants_order:
        if model not in models_payload:
            continue
        block = models_payload[model]
        q = block.get("quality", {})
        c = block.get("coverageDiversityNovelty", {})
        rows.append(
            [
                model,
                str(q.get("evaluatedUsers", 0)),
                f"{float(q.get(pk, 0)):.4f}",
                f"{float(q.get(rk, 0)):.4f}",
                f"{float(q.get(nk, 0)):.4f}",
                f"{float(q.get(hk, 0)):.4f}",
                f"{float(c.get('catalogCoverage', 0)):.4f}",
                f"{float(c.get('diversity', 0)):.4f}",
                f"{float(c.get('novelty', 0)):.4f}",
            ]
        )
    widths = [max(len(headers[i]), max((len(r[i]) for r in rows), default=0)) for i in range(len(headers))]
    sep = " | "

    def fmt_row(cells: List[str]) -> str:
        return sep.join(c.ljust(widths[i]) for i, c in enumerate(cells))

    print()
    print(fmt_row(headers))
    print(sep.join("-" * widths[i] for i in range(len(headers))))
    for r in rows:
        print(fmt_row(r))
    print()


def main() -> None:
    parser = argparse.ArgumentParser(description="Offline evaluation for OULAD (multi-strategy).")
    parser.add_argument("--processed-dir", type=Path, default=DEFAULT_PROCESSED_DIR)
    parser.add_argument(
        "--variants",
        nargs="*",
        default=None,
        metavar="VARIANT",
        help=f"Strategies to run (default: all). Choices: {', '.join(VALID_VARIANTS)}",
    )
    parser.add_argument("--k", type=int, default=10)
    parser.add_argument("--max-users", type=int, default=0)
    parser.add_argument("--min-test-completed", type=int, default=1)
    parser.add_argument(
        "--sort-users-by-test-size",
        action="store_true",
        help="Evaluate users with most completed test interactions first.",
    )
    parser.add_argument("--backend-url", default="http://localhost:5235")
    parser.add_argument("--ai-url", default="http://127.0.0.1:8001")
    parser.add_argument(
        "--output-prefix",
        default="",
        help="Directory to write evaluation_report.json, evaluation_per_user.csv, evaluation_summary.csv "
        "(default: same as --processed-dir).",
    )
    parser.add_argument("--timeout-s", type=int, default=30)
    args = parser.parse_args()

    variants = parse_variants_arg(args.variants)
    processed_dir: Path = args.processed_dir
    out_dir = resolve_output_dir(processed_dir, args.output_prefix.strip() or None)

    resources = load_json_array(processed_dir / "resources.json")
    train = load_json_array(processed_dir / "interactions_train.json")
    test = load_json_array(processed_dir / "interactions_test.json")
    resource_by_id = {str(r["id"]): r for r in resources if r.get("id") is not None}
    catalog_size = len(resource_by_id)

    # Ground truth: Completed only in test split
    relevant_by_user: Dict[str, Set[str]] = defaultdict(set)
    for rec in test:
        if rec.get("interactionType") != "Completed":
            continue
        user_id = str(rec["userId"])
        resource_id = str(rec["learningResourceId"])
        relevant_by_user[user_id].add(resource_id)

    train_completed_count_by_user: Dict[str, int] = defaultdict(int)
    user_completed_train: Dict[str, Set[str]] = defaultdict(set)
    completed_counts: Dict[str, int] = defaultdict(int)
    interaction_counts: Dict[str, int] = defaultdict(int)
    train_completed_rows = 0
    for rec in train:
        user_id = str(rec.get("userId", ""))
        rid = str(rec.get("learningResourceId", ""))
        if rec.get("interactionType") == "Completed":
            train_completed_rows += 1
            if user_id:
                train_completed_count_by_user[user_id] += 1
                user_completed_train[user_id].add(rid)
            if rid:
                completed_counts[rid] += 1
                interaction_counts[rid] += 1
    total_interactions = sum(interaction_counts.values())

    candidate_users = [
        u for u, rel in relevant_by_user.items() if len(rel) >= max(1, args.min_test_completed)
    ]
    candidate_users = sorted(candidate_users)
    if args.sort_users_by_test_size:
        candidate_users.sort(key=lambda u: len(relevant_by_user[u]), reverse=True)
    if args.max_users > 0:
        candidate_users = candidate_users[: args.max_users]

    if not candidate_users:
        raise SystemExit("No users with Completed interactions in test set.")

    backend = args.backend_url.rstrip("/")
    ai = args.ai_url.rstrip("/")
    session = requests.Session()
    k = args.k

    # Per-strategy recommendation lists (only successful hybrid users get entries for that strategy)
    rec_lists: Dict[str, Dict[str, List[str]]] = {v: {} for v in variants}

    hybrid_variants_needed = [v for v in variants if v.startswith("hybrid_")]
    ai_variant_param = {
        "hybrid_full": "full",
        "hybrid_no_difficulty": "no_difficulty",
    }

    for idx, user_id in enumerate(candidate_users, start=1):
        print(f"[{idx}/{len(candidate_users)}] User {user_id}")

        for strat in hybrid_variants_needed:
            param = ai_variant_param[strat]
            print(f"  -> {strat} (POST generate ?variant={param})")
            recs = fetch_hybrid_recommendations(
                session,
                backend,
                ai,
                user_id,
                k,
                param,
                args.timeout_s,
                user_completed_train,
            )
            if recs is None:
                print(f"     skip: AI/backend failed for {user_id}")
            else:
                rec_lists[strat][user_id] = recs

    evaluated_users: Set[str] = set(candidate_users)
    for strat in hybrid_variants_needed:
        evaluated_users &= set(rec_lists[strat].keys())

    if not evaluated_users:
        raise SystemExit("No users evaluated successfully for all requested hybrid variants.")

    evaluated_user_ids = sorted(evaluated_users)

    if "popularity_baseline" in variants:
        rec_lists["popularity_baseline"] = build_popularity_recommendations(
            user_ids=evaluated_user_ids,
            completed_counts=completed_counts,
            user_completed_train=user_completed_train,
            catalog_resource_ids=set(resource_by_id.keys()),
            k=k,
        )

    per_user_by_model: Dict[str, List[dict]] = {}
    for strat in variants:
        lists = {uid: rec_lists[strat].get(uid, []) for uid in evaluated_user_ids}
        per_user_by_model[strat] = evaluate_rows_from_recommendations(
            user_ids=evaluated_user_ids,
            recommended_lists=lists,
            relevant_by_user=relevant_by_user,
            train_completed_count_by_user=train_completed_count_by_user,
            k=k,
        )

    models_payload: Dict[str, dict] = {}
    for strat in variants:
        rows = per_user_by_model[strat]
        lists = {uid: rec_lists[strat][uid] for uid in evaluated_user_ids}
        label = strat
        q = evaluate_quality_experiment(rows, k, label)
        cold = evaluate_cold_start_experiment(rows, k, label)
        cov = evaluate_coverage_diversity_novelty(
            rows=rows,
            recommended_lists=lists,
            resource_by_id=resource_by_id,
            interaction_counts=dict(interaction_counts),
            total_interactions=total_interactions,
            catalog_size=catalog_size,
            k=k,
            label=label,
        )
        models_payload[strat] = {
            "quality": q,
            "coldStart": cold,
            "coverageDiversityNovelty": cov,
        }

    comparison_deltas: Dict[str, dict] = {}
    if "hybrid_full" in variants and "popularity_baseline" in variants:
        a = merge_quality_and_distribution(
            models_payload["hybrid_full"]["quality"],
            models_payload["hybrid_full"]["coverageDiversityNovelty"],
            k,
        )
        b = merge_quality_and_distribution(
            models_payload["popularity_baseline"]["quality"],
            models_payload["popularity_baseline"]["coverageDiversityNovelty"],
            k,
        )
        comparison_deltas["hybrid_full_minus_popularity_baseline"] = subtract_metrics(a, b, k)

    if "hybrid_full" in variants and "hybrid_no_difficulty" in variants:
        a = merge_quality_and_distribution(
            models_payload["hybrid_full"]["quality"],
            models_payload["hybrid_full"]["coverageDiversityNovelty"],
            k,
        )
        b = merge_quality_and_distribution(
            models_payload["hybrid_no_difficulty"]["quality"],
            models_payload["hybrid_no_difficulty"]["coverageDiversityNovelty"],
            k,
        )
        comparison_deltas["hybrid_full_minus_hybrid_no_difficulty"] = subtract_metrics(a, b, k)

    if "hybrid_no_difficulty" in variants and "popularity_baseline" in variants:
        a = merge_quality_and_distribution(
            models_payload["hybrid_no_difficulty"]["quality"],
            models_payload["hybrid_no_difficulty"]["coverageDiversityNovelty"],
            k,
        )
        b = merge_quality_and_distribution(
            models_payload["popularity_baseline"]["quality"],
            models_payload["popularity_baseline"]["coverageDiversityNovelty"],
            k,
        )
        comparison_deltas["hybrid_no_difficulty_minus_popularity_baseline"] = subtract_metrics(a, b, k)

    test_completed_rows = sum(1 for r in test if r.get("interactionType") == "Completed")

    experiment_config = {
        "processedDir": str(processed_dir),
        "outputDir": str(out_dir),
        "backendUrl": args.backend_url,
        "aiUrl": args.ai_url,
        "variants": variants,
        "k": k,
        "minTestCompleted": max(1, args.min_test_completed),
        "maxUsers": args.max_users,
        "sortUsersByTestSize": bool(args.sort_users_by_test_size),
        "timeoutSeconds": args.timeout_s,
    }

    dataset_statistics = {
        "catalogResourceCount": catalog_size,
        "trainInteractionCount": len(train),
        "trainCompletedInteractionCount": train_completed_rows,
        "testInteractionCount": len(test),
        "testCompletedInteractionCount": test_completed_rows,
        "testUsersWithRelevantCompleted": len(relevant_by_user),
        "candidateUsersAfterFilters": len(candidate_users),
        "evaluatedUsersAllStrategies": len(evaluated_user_ids),
        "trainGlobalInteractionEventCount": total_interactions,
    }

    summary_top = {
        "experimentConfig": experiment_config,
        "datasetStatistics": dataset_statistics,
        "k": k,
        "models": models_payload,
        "comparisonDeltas": comparison_deltas,
    }

    out_json = out_dir / "evaluation_report.json"
    out_csv_per_user = out_dir / "evaluation_per_user.csv"
    out_csv_summary = out_dir / "evaluation_summary.csv"

    with out_json.open("w", encoding="utf-8") as f:
        json.dump(
            {
                "summary": summary_top,
                "perUserByModel": per_user_by_model,
            },
            f,
            indent=2,
        )

    merged_rows: List[dict] = []
    for strat in variants:
        for row in per_user_by_model[strat]:
            item = dict(row)
            item["model"] = strat
            merged_rows.append(item)

    csv_fieldnames = [
        "model",
        "userId",
        "relevantCount",
        "recommendedCount",
        "trainCompletedCount",
        f"precision@{k}",
        f"recall@{k}",
        f"hitRate@{k}",
        f"ndcg@{k}",
    ]
    with out_csv_per_user.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=csv_fieldnames)
        writer.writeheader()
        writer.writerows(merged_rows)

    summary_csv_rows: List[dict] = []
    pk, rk, nk, hk = f"precision@{k}", f"recall@{k}", f"ndcg@{k}", f"hitRate@{k}"
    for strat in variants:
        q = models_payload[strat]["quality"]
        c = models_payload[strat]["coverageDiversityNovelty"]
        summary_csv_rows.append(
            {
                "model": strat,
                "evaluatedUsers": q.get("evaluatedUsers", 0),
                pk: q.get(pk, 0.0),
                rk: q.get(rk, 0.0),
                nk: q.get(nk, 0.0),
                hk: q.get(hk, 0.0),
                "catalogCoverage": c.get("catalogCoverage", 0.0),
                "diversity": c.get("diversity", 0.0),
                "novelty": c.get("novelty", 0.0),
            }
        )

    summary_fieldnames = [
        "model",
        "evaluatedUsers",
        pk,
        rk,
        nk,
        hk,
        "catalogCoverage",
        "diversity",
        "novelty",
    ]
    with out_csv_summary.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=summary_fieldnames)
        writer.writeheader()
        writer.writerows(summary_csv_rows)

    print_summary_table(models_payload, k, variants)

    print("Comparison deltas (quality + distribution):")
    for name, delta in comparison_deltas.items():
        print(f"  {name}:")
        for dk, dv in sorted(delta.items()):
            print(f"    {dk}: {dv:+.6f}")
    if not comparison_deltas:
        print("  (none — need both models in a pair for requested variants)")

    print(f"\nSaved: {out_json}")
    print(f"Saved: {out_csv_per_user}")
    print(f"Saved: {out_csv_summary}")


if __name__ == "__main__":
    main()
