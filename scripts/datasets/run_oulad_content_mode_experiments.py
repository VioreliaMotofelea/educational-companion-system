#!/usr/bin/env python3
"""
Run the 7-row OULAD content-mode experiment matrix in-process (no AI env restarts).

Compares popularity baseline vs hybrid (full / no_difficulty) × content fusion
(tfidf_only, semantic_only, tfidf_semantic) on the SAME evaluated user sample.

See scripts/datasets/OULAD_CONTENT_MODE_EXPERIMENTS.md for usage and interpretation.
"""

from __future__ import annotations

import argparse
import csv
import json
import random
import sys
import time
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, List, Sequence, Set

import numpy as np

ROOT_DIR = Path(__file__).resolve().parents[2]
SCRIPTS_DATASETS_DIR = Path(__file__).resolve().parent
AI_SERVICE_DIR = ROOT_DIR / "ai-service"
for path in (str(AI_SERVICE_DIR), str(SCRIPTS_DATASETS_DIR)):
    if path not in sys.path:
        sys.path.insert(0, path)

from recommender.content_overrides import ContentModeOverrides, overrides_for_fusion_mode  # noqa: E402
from recommender.hybrid import (  # noqa: E402
    build_all_fusion_content_maps,
    build_collaborative_score_map,
    build_global_hybrid_context,
    rank_hybrid_from_score_maps,
)
from recommender.content_based import prewarm_tfidf_matrix  # noqa: E402
from recommender.collaborative import prepare_collaborative_matrix  # noqa: E402

# Reuse metric helpers from the existing offline evaluator.
from evaluate_oulad_offline import (  # noqa: E402
    DEFAULT_PROCESSED_DIR,
    build_popularity_recommendations,
    evaluate_coverage_diversity_novelty,
    evaluate_quality_experiment,
    evaluate_rows_from_recommendations,
    filter_recommendations_exclude_train,
    load_json_array,
)

FUSION_MODES: tuple[str, ...] = ("tfidf_only", "semantic_only", "tfidf_semantic")
HYBRID_VARIANTS: tuple[str, ...] = ("full", "no_difficulty")

DEFAULT_EXPERIMENT_LABELS: tuple[str, ...] = (
    "popularity_baseline",
    "hybrid_full__tfidf_only",
    "hybrid_full__semantic_only",
    "hybrid_full__tfidf_semantic",
    "hybrid_no_difficulty__tfidf_only",
    "hybrid_no_difficulty__semantic_only",
    "hybrid_no_difficulty__tfidf_semantic",
)


class PhaseTimer:
    """Lightweight phase timing for long offline runs."""

    def __init__(self) -> None:
        self._t0 = time.perf_counter()
        self._last = self._t0
        self.phases: dict[str, float] = {}

    def mark(self, label: str) -> None:
        now = time.perf_counter()
        self.phases[label] = now - self._last
        self._last = now
        print(f"  timing: {label} {self.phases[label]:.2f}s")

    @property
    def total_seconds(self) -> float:
        return time.perf_counter() - self._t0


@dataclass(frozen=True)
class ExperimentSpec:
    label: str
    kind: str  # popularity | hybrid
    hybrid_variant: str | None = None
    fusion_mode: str | None = None

    def content_overrides(
        self,
        *,
        semantic_model_name: str | None,
        tfidf_subweight: float | None,
        semantic_subweight: float | None,
    ) -> ContentModeOverrides | None:
        if self.kind != "hybrid" or not self.fusion_mode:
            return None
        base = overrides_for_fusion_mode(self.fusion_mode, semantic_model_name=semantic_model_name)
        if tfidf_subweight is None and semantic_subweight is None:
            return base
        return ContentModeOverrides(
            semantic_enabled=base.semantic_enabled,
            content_fusion_mode=base.content_fusion_mode,
            content_tfidf_subweight=tfidf_subweight,
            content_semantic_subweight=semantic_subweight,
            semantic_model_name=base.semantic_model_name,
        )


def default_experiment_matrix() -> List[ExperimentSpec]:
    specs: List[ExperimentSpec] = [
        ExperimentSpec(label="popularity_baseline", kind="popularity"),
    ]
    for variant in HYBRID_VARIANTS:
        for fusion in FUSION_MODES:
            specs.append(
                ExperimentSpec(
                    label=f"hybrid_{variant}__{fusion}",
                    kind="hybrid",
                    hybrid_variant=variant,
                    fusion_mode=fusion,
                )
            )
    return specs


def parse_experiment_labels(raw: Sequence[str] | None) -> List[ExperimentSpec]:
    by_label = {s.label: s for s in default_experiment_matrix()}
    if not raw:
        return list(by_label.values())
    unknown = [x for x in raw if x not in by_label]
    if unknown:
        raise SystemExit(
            f"Unknown experiment label(s): {unknown}. "
            f"Choose from: {', '.join(DEFAULT_EXPERIMENT_LABELS)}"
        )
    return [by_label[x] for x in raw]


def select_evaluated_users(
    candidate_users: List[str],
    *,
    max_users: int,
    sort_by_test_size: bool,
    relevant_by_user: Dict[str, Set[str]],
    user_sample_seed: int | None,
) -> List[str]:
    users = list(candidate_users)
    if sort_by_test_size:
        users.sort(key=lambda u: len(relevant_by_user[u]), reverse=True)
    else:
        users.sort()

    if max_users <= 0 or max_users >= len(users):
        return users

    if user_sample_seed is not None:
        rng = random.Random(user_sample_seed)
        rng.shuffle(users)
        return sorted(users[:max_users])

    return users[:max_users]


def validate_experiment_inputs(*, k: int, resources: List[dict]) -> None:
    if k <= 0:
        raise SystemExit("--k must be > 0")
    if not resources:
        raise SystemExit("resources.json is empty or missing entries")


def assert_rec_lists_complete(
    rec_lists: Dict[str, Dict[str, List[str]]],
    evaluated_user_ids: List[str],
    spec_labels: Sequence[str],
) -> None:
    """Ensure every experiment row has an entry for every evaluated user."""
    expected = set(evaluated_user_ids)
    for label in spec_labels:
        if label not in rec_lists:
            raise RuntimeError(f"Missing recommendation lists for experiment: {label}")
        missing = expected - set(rec_lists[label].keys())
        if missing:
            sample = sorted(missing)[:3]
            raise RuntimeError(
                f"Experiment {label} missing {len(missing)} user(s), e.g. {sample}"
            )


def build_user_context(user_row: dict) -> tuple[dict, dict]:
    level = max(1, min(5, int(user_row.get("level", 2))))
    user = {
        "userId": str(user_row["userId"]),
        "level": level,
        "preferences": {"preferredDifficulty": level},
    }
    mastery = {"suggestedDifficulty": level}
    return user, mastery


def difficulty_metrics_at_k(
    recommended: List[str],
    resource_by_id: Dict[str, dict],
    suggested_difficulty: int,
    k: int,
    acceptable_band: int = 1,
) -> tuple[float, float]:
    top_k = recommended[:k]
    if not top_k:
        return 0.0, 0.0

    gaps: List[float] = []
    in_band = 0
    for rid in top_k:
        resource = resource_by_id.get(rid)
        if not resource:
            continue
        diff = int(resource.get("difficulty", 1))
        gap = abs(diff - suggested_difficulty)
        gaps.append(float(gap))
        if gap <= acceptable_band:
            in_band += 1

    if not gaps:
        return 0.0, 0.0

    return sum(gaps) / len(gaps), in_band / len(top_k)


def prewarm_evaluation_caches(
    resources: List[dict],
    all_train_interactions: List[dict],
    semantic_model_name: str,
    needed_fusions: Set[str],
    timer: PhaseTimer,
) -> tuple[Any, ...]:
    """Load shared TF-IDF, semantic, and collaborative structures once."""
    print("Prewarming TF-IDF matrix...")
    prewarm_tfidf_matrix(resources)
    timer.mark("prewarm_tfidf")

    if needed_fusions & {"semantic_only", "tfidf_semantic"}:
        from recommender.content_semantic import _get_resource_embeddings

        print("Prewarming semantic embeddings for catalog...")
        _get_resource_embeddings(resources, semantic_model_name)
        timer.mark("prewarm_semantic")

    print("Prewarming collaborative matrix...")
    collab_prepared = prepare_collaborative_matrix(all_train_interactions, resources)
    timer.mark("prewarm_collaborative")
    return collab_prepared


def run_hybrid_experiments_for_user(
    *,
    user_id: str,
    user: dict,
    suggested_difficulty: int,
    user_interactions: List[dict],
    resources: List[dict],
    popularity_map: dict[str, int],
    max_popularity: int,
    hybrid_specs: List[ExperimentSpec],
    collab_prepared: tuple[Any, ...],
    k: int,
    semantic_model_name: str,
    tfidf_subweight: float | None,
    semantic_subweight: float | None,
) -> Dict[str, List[str]]:
    """Score once per user; emit recommendation ids for each hybrid experiment row."""
    completed_ids = {
        str(i["learningResourceId"])
        for i in user_interactions
        if i.get("interactionType") == "Completed"
    }

    collab_map = build_collaborative_score_map(
        user_id,
        [],
        resources,
        prepared=collab_prepared,
    )

    needed_fusions = {str(s.fusion_mode) for s in hybrid_specs if s.fusion_mode}
    tfidf_w = tfidf_subweight if tfidf_subweight is not None else 0.5
    semantic_w = semantic_subweight if semantic_subweight is not None else 0.5

    if needed_fusions == set(FUSION_MODES):
        content_by_fusion = build_all_fusion_content_maps(
            user,
            user_interactions,
            resources,
            semantic_model_name=semantic_model_name,
            tfidf_subweight=tfidf_w,
            semantic_subweight=semantic_w,
        )
    else:
        from recommender.hybrid import _build_content_score_map

        content_by_fusion = {}
        for fusion in needed_fusions:
            overrides = overrides_for_fusion_mode(fusion, semantic_model_name=semantic_model_name)
            if tfidf_subweight is not None or semantic_subweight is not None:
                overrides = ContentModeOverrides(
                    semantic_enabled=overrides.semantic_enabled,
                    content_fusion_mode=overrides.content_fusion_mode,
                    content_tfidf_subweight=tfidf_subweight,
                    content_semantic_subweight=semantic_subweight,
                    semantic_model_name=overrides.semantic_model_name,
                )
            content_by_fusion[fusion] = _build_content_score_map(
                user, user_interactions, resources, content_overrides=overrides
            )

    results: Dict[str, List[str]] = {}
    for spec in hybrid_specs:
        fusion = str(spec.fusion_mode)
        content_map, content_mode = content_by_fusion[fusion]
        rec_ids = rank_hybrid_from_score_maps(
            user=user,
            resources=resources,
            completed_ids=completed_ids,
            content_map=content_map,
            content_mode=content_mode,
            collab_map=collab_map,
            popularity_map=popularity_map,
            max_popularity=max_popularity,
            suggested_difficulty=suggested_difficulty,
            variant=str(spec.hybrid_variant),
            top_k=k,
            ids_only=True,
        )
        results[spec.label] = list(rec_ids)

    return results


def merge_summary_row(
    label: str,
    quality: dict,
    coverage: dict,
    difficulty_avg_gap: float,
    difficulty_in_band: float,
    k: int,
) -> dict:
    pk, rk, nk, hk = f"precision@{k}", f"recall@{k}", f"ndcg@{k}", f"hitRate@{k}"
    return {
        "experiment": label,
        "evaluatedUsers": int(quality.get("evaluatedUsers", 0)),
        pk: float(quality.get(pk, 0.0)),
        rk: float(quality.get(rk, 0.0)),
        nk: float(quality.get(nk, 0.0)),
        hk: float(quality.get(hk, 0.0)),
        "catalogCoverage": float(coverage.get("catalogCoverage", 0.0)),
        "diversity": float(coverage.get("diversity", 0.0)),
        "novelty": float(coverage.get("novelty", 0.0)),
        f"avgDifficultyGap@{k}": difficulty_avg_gap,
        f"difficultyInBandRate@{k}": difficulty_in_band,
    }


def write_latex_table(summary_rows: List[dict], k: int, out_path: Path) -> None:
    pk, rk, nk, hk = f"precision@{k}", f"recall@{k}", f"ndcg@{k}", f"hitRate@{k}"
    lines = [
        "% Auto-generated by run_oulad_content_mode_experiments.py",
        "\\begin{table}[ht]",
        "\\centering",
        "\\small",
        "\\begin{tabular}{lrrrrrrr}",
        "\\hline",
        "Experiment & P@" + str(k) + " & R@" + str(k) + " & nDCG@" + str(k) + " & Hit@" + str(k)
        + " & Cov. & Div. & Nov. \\\\",
        "\\hline",
    ]
    for row in summary_rows:
        label = str(row["experiment"]).replace("_", "\\_")
        lines.append(
            f"{label} & {row[pk]:.4f} & {row[rk]:.4f} & {row[nk]:.4f} & {row[hk]:.4f} "
            f"& {row['catalogCoverage']:.4f} & {row['diversity']:.4f} & {row['novelty']:.4f} \\\\"
        )
    lines.extend(["\\hline", "\\end{tabular}", "\\caption{OULAD content-mode experiment matrix}", "\\end{table}", ""])
    out_path.write_text("\n".join(lines), encoding="utf-8")


def _short_label(label: str) -> str:
    return label.replace("hybrid_", "H:").replace("__", "\n").replace("_", " ")


def write_plots(summary_rows: List[dict], k: int, figures_dir: Path) -> None:
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    figures_dir.mkdir(parents=True, exist_ok=True)
    labels = [str(r["experiment"]) for r in summary_rows]
    short = [_short_label(x) for x in labels]
    x = np.arange(len(labels))

    pk, rk, nk, hk = f"precision@{k}", f"recall@{k}", f"ndcg@{k}", f"hitRate@{k}"

    # Phase 1 — content modes under hybrid_full (+ popularity)
    phase1_labels = [
        r for r in labels
        if r == "popularity_baseline" or r.startswith("hybrid_full__")
    ]
    if phase1_labels:
        idx = [labels.index(l) for l in phase1_labels]
        subset = [summary_rows[i] for i in idx]
        bx = np.arange(len(idx))
        fig, ax = plt.subplots(figsize=(max(8, len(idx) * 1.1), 5))
        width = 0.2
        metrics = [pk, rk, nk, hk]
        titles = ["Precision", "Recall", "nDCG", "Hit rate"]
        for i, (metric, title) in enumerate(zip(metrics, titles)):
            ax.bar(bx + (i - 1.5) * width, [subset[j][metric] for j in range(len(idx))], width, label=title)
        ax.set_xticks(bx)
        ax.set_xticklabels([_short_label(l) for l in phase1_labels], rotation=35, ha="right", fontsize=8)
        ax.set_ylabel("Score")
        ax.set_title(f"Ranking metrics @ k={k} (Phase 1: content modes)")
        ax.legend()
        ax.grid(axis="y", alpha=0.3)
        fig.tight_layout()
        fig.savefig(figures_dir / "ranking_metrics_comparison.png", dpi=150)
        plt.close(fig)

    # Beyond-accuracy (all rows)
    fig, ax = plt.subplots(figsize=(max(10, len(labels) * 0.55), 5))
    width = 0.25
    for i, (metric, title) in enumerate(
        [("catalogCoverage", "Coverage"), ("diversity", "Diversity"), ("novelty", "Novelty")]
    ):
        ax.bar(x + (i - 1) * width, [r[metric] for r in summary_rows], width, label=title)
    ax.set_xticks(x)
    ax.set_xticklabels(short, rotation=45, ha="right", fontsize=7)
    ax.set_title("Beyond-accuracy metrics")
    ax.legend()
    ax.grid(axis="y", alpha=0.3)
    fig.tight_layout()
    fig.savefig(figures_dir / "beyond_accuracy_comparison.png", dpi=150)
    plt.close(fig)

    # Difficulty ablation: full minus no_difficulty per fusion mode
    ablation_rows: List[dict] = []
    ablation_labels: List[str] = []
    for fusion in FUSION_MODES:
        full_label = f"hybrid_full__{fusion}"
        nodiff_label = f"hybrid_no_difficulty__{fusion}"
        full_row = next((r for r in summary_rows if r["experiment"] == full_label), None)
        nodiff_row = next((r for r in summary_rows if r["experiment"] == nodiff_label), None)
        if full_row and nodiff_row:
            ablation_labels.append(fusion)
            ablation_rows.append(
                {
                    pk: full_row[pk] - nodiff_row[pk],
                    rk: full_row[rk] - nodiff_row[rk],
                    nk: full_row[nk] - nodiff_row[nk],
                    hk: full_row[hk] - nodiff_row[hk],
                }
            )

    if ablation_rows:
        fig, ax = plt.subplots(figsize=(8, 4.5))
        bx = np.arange(len(ablation_labels))
        width = 0.2
        for i, (metric, title) in enumerate(zip([pk, rk, nk, hk], ["P", "R", "nDCG", "Hit"])):
            ax.bar(bx + (i - 1.5) * width, [r[metric] for r in ablation_rows], width, label=title)
        ax.axhline(0.0, color="black", linewidth=0.8)
        ax.set_xticks(bx)
        ax.set_xticklabels(ablation_labels)
        ax.set_title(f"Difficulty ablation delta (full − no_difficulty) @ k={k}")
        ax.legend()
        ax.grid(axis="y", alpha=0.3)
        fig.tight_layout()
        fig.savefig(figures_dir / "difficulty_ablation_comparison.png", dpi=150)
        plt.close(fig)


def print_summary_table(summary_rows: List[dict], k: int) -> None:
    pk, rk, nk, hk = f"precision@{k}", f"recall@{k}", f"ndcg@{k}", f"hitRate@{k}"
    headers = ["experiment", "users", pk, rk, nk, hk, "coverage", "diversity", "novelty"]
    rows: List[List[str]] = []
    for row in summary_rows:
        rows.append(
            [
                str(row["experiment"]),
                str(row["evaluatedUsers"]),
                f"{row[pk]:.4f}",
                f"{row[rk]:.4f}",
                f"{row[nk]:.4f}",
                f"{row[hk]:.4f}",
                f"{row['catalogCoverage']:.4f}",
                f"{row['diversity']:.4f}",
                f"{row['novelty']:.4f}",
            ]
        )
    widths = [max(len(headers[i]), max((len(r[i]) for r in rows), default=0)) for i in range(len(headers))]
    sep = " | "

    def fmt(cells: List[str]) -> str:
        return sep.join(c.ljust(widths[i]) for i, c in enumerate(cells))

    print()
    print(fmt(headers))
    print(sep.join("-" * w for w in widths))
    for r in rows:
        print(fmt(r))
    print()


def main() -> None:
    parser = argparse.ArgumentParser(
        description="OULAD 7-row content-mode experiment matrix (in-process, same user sample)."
    )
    parser.add_argument("--processed-dir", type=Path, default=DEFAULT_PROCESSED_DIR)
    parser.add_argument(
        "--run-tag",
        default="eval_content_matrix",
        help="Output folder name under processed-dir (e.g. eval_n3000_tfidf_only is NOT used here; "
        "one folder holds all 7 rows).",
    )
    parser.add_argument(
        "--experiments",
        nargs="*",
        default=None,
        metavar="LABEL",
        help=f"Subset of experiment labels (default: all 7). Choices: {', '.join(DEFAULT_EXPERIMENT_LABELS)}",
    )
    parser.add_argument("--k", type=int, default=10)
    parser.add_argument(
        "--max-users",
        type=int,
        default=0,
        help="Cap evaluated users (0 = all eligible users with test Completed ground truth).",
    )
    parser.add_argument("--min-test-completed", type=int, default=1)
    parser.add_argument("--sort-users-by-test-size", action="store_true")
    parser.add_argument(
        "--user-sample-seed",
        type=int,
        default=None,
        help="When --max-users caps the pool, randomly sample with this seed (otherwise take first N after sort).",
    )
    parser.add_argument("--semantic-model-name", default="sentence-transformers/all-MiniLM-L6-v2")
    parser.add_argument("--content-tfidf-subweight", type=float, default=None)
    parser.add_argument("--content-semantic-subweight", type=float, default=None)
    parser.add_argument("--difficulty-band", type=int, default=1)
    args = parser.parse_args()

    processed_dir: Path = args.processed_dir
    out_dir = processed_dir / args.run_tag
    out_dir.mkdir(parents=True, exist_ok=True)
    figures_dir = out_dir / "figures"

    specs = parse_experiment_labels(args.experiments)
    k = args.k
    if k <= 0:
        raise SystemExit("--k must be > 0")
    timer = PhaseTimer()

    print("Loading processed OULAD JSON...")
    resources = load_json_array(processed_dir / "resources.json")
    users = load_json_array(processed_dir / "users.json")
    train = load_json_array(processed_dir / "interactions_train.json")
    test = load_json_array(processed_dir / "interactions_test.json")
    validate_experiment_inputs(k=k, resources=resources)
    timer.mark("load_json")

    user_by_id = {str(u["userId"]): u for u in users}
    resource_by_id = {str(r["id"]): r for r in resources if r.get("id") is not None}
    catalog_size = len(resource_by_id)

    relevant_by_user: Dict[str, Set[str]] = defaultdict(set)
    for rec in test:
        if rec.get("interactionType") != "Completed":
            continue
        relevant_by_user[str(rec["userId"])].add(str(rec["learningResourceId"]))

    train_completed_count_by_user: Dict[str, int] = defaultdict(int)
    user_completed_train: Dict[str, Set[str]] = defaultdict(set)
    completed_counts: Dict[str, int] = defaultdict(int)
    # Completed train counts — same policy as hybrid reranking and novelty@k metrics.
    interaction_counts: Dict[str, int] = defaultdict(int)
    interactions_by_user: Dict[str, List[dict]] = defaultdict(list)

    for rec in train:
        user_id = str(rec.get("userId", ""))
        rid = str(rec.get("learningResourceId", ""))
        interactions_by_user[user_id].append(rec)
        if rec.get("interactionType") == "Completed":
            train_completed_count_by_user[user_id] += 1
            user_completed_train[user_id].add(rid)
            completed_counts[rid] += 1
            interaction_counts[rid] += 1

    total_interactions = sum(interaction_counts.values())
    all_train_interactions = list(train)

    user_context_cache: Dict[str, tuple[dict, int]] = {}
    for user_row in users:
        user_dict, mastery = build_user_context(user_row)
        user_context_cache[str(user_row["userId"])] = (user_dict, int(mastery["suggestedDifficulty"]))

    candidate_users = [
        u for u, rel in relevant_by_user.items() if len(rel) >= max(1, args.min_test_completed)
    ]
    evaluated_user_ids = select_evaluated_users(
        candidate_users,
        max_users=args.max_users,
        sort_by_test_size=bool(args.sort_users_by_test_size),
        relevant_by_user=relevant_by_user,
        user_sample_seed=args.user_sample_seed,
    )

    if not evaluated_user_ids:
        raise SystemExit("No eligible users with Completed test interactions.")

    evaluated_users_path = out_dir / "evaluated_users.json"
    evaluated_users_path.write_text(
        json.dumps(evaluated_user_ids, indent=2),
        encoding="utf-8",
    )

    experiment_config = {
        "processedDir": str(processed_dir),
        "outputDir": str(out_dir),
        "runTag": args.run_tag,
        "experiments": [s.label for s in specs],
        "k": k,
        "maxUsers": args.max_users,
        "minTestCompleted": args.min_test_completed,
        "sortUsersByTestSize": bool(args.sort_users_by_test_size),
        "userSampleSeed": args.user_sample_seed,
        "semanticModelName": args.semantic_model_name,
        "contentTfidfSubweight": args.content_tfidf_subweight,
        "contentSemanticSubweight": args.content_semantic_subweight,
        "difficultyBand": args.difficulty_band,
        "evaluatedUserCount": len(evaluated_user_ids),
        "candidateUserCount": len(candidate_users),
        "catalogResourceCount": catalog_size,
        "trainInteractionCount": len(train),
        "testInteractionCount": len(test),
        "evaluationMode": "in_process_batch_optimized",
    }
    (out_dir / "experiment_config.json").write_text(
        json.dumps(experiment_config, indent=2),
        encoding="utf-8",
    )

    hybrid_specs = [s for s in specs if s.kind == "hybrid"]
    popularity_needed = any(s.kind == "popularity" for s in specs)

    popularity_map, max_popularity = build_global_hybrid_context(all_train_interactions, resources)
    collab_prepared: tuple[Any, ...] = ()
    if hybrid_specs:
        needed_fusions = {str(s.fusion_mode) for s in hybrid_specs if s.fusion_mode}
        collab_prepared = prewarm_evaluation_caches(
            resources,
            all_train_interactions,
            args.semantic_model_name,
            needed_fusions,
            timer,
        )

    print(f"Evaluating {len(evaluated_user_ids)} users (candidate pool: {len(candidate_users)})")
    print(f"Experiments: {[s.label for s in specs]}")
    print(f"Output: {out_dir}\n")

    rec_lists: Dict[str, Dict[str, List[str]]] = {s.label: {} for s in specs}
    hybrid_t0 = time.perf_counter()

    if popularity_needed:
        print("=== popularity_baseline ===")
        rec_lists["popularity_baseline"] = build_popularity_recommendations(
            user_ids=evaluated_user_ids,
            completed_counts=completed_counts,
            user_completed_train=user_completed_train,
            catalog_resource_ids=set(resource_by_id.keys()),
            k=k,
        )
        print(f"  popularity lists built for {len(rec_lists['popularity_baseline'])} users")

    skipped_no_train_completed = 0
    for idx, user_id in enumerate(evaluated_user_ids, start=1):
        if hybrid_specs and (idx == 1 or idx % 100 == 0 or idx == len(evaluated_user_ids)):
            elapsed = time.perf_counter() - hybrid_t0
            rate = idx / elapsed if elapsed > 0 else 0.0
            remaining = (len(evaluated_user_ids) - idx) / rate if rate > 0 else 0.0
            print(
                f"[{idx}/{len(evaluated_user_ids)}] {user_id} "
                f"({elapsed:.0f}s elapsed, ~{remaining / 60:.1f} min remaining)"
            )

        if not hybrid_specs:
            continue

        user_interactions = interactions_by_user.get(user_id, [])
        if not any(i.get("interactionType") == "Completed" for i in user_interactions):
            skipped_no_train_completed += 1
            for spec in hybrid_specs:
                rec_lists[spec.label][user_id] = []
            continue

        user_row = user_by_id.get(user_id, {"userId": user_id, "level": 2})
        user, suggested = user_context_cache.get(user_id, build_user_context(user_row))
        per_spec = run_hybrid_experiments_for_user(
            user_id=user_id,
            user=user,
            suggested_difficulty=suggested,
            user_interactions=user_interactions,
            resources=resources,
            popularity_map=popularity_map,
            max_popularity=max_popularity,
            hybrid_specs=hybrid_specs,
            collab_prepared=collab_prepared,
            k=k,
            semantic_model_name=args.semantic_model_name,
            tfidf_subweight=args.content_tfidf_subweight,
            semantic_subweight=args.content_semantic_subweight,
        )
        for label, raw_recs in per_spec.items():
            rec_lists[label][user_id] = filter_recommendations_exclude_train(
                raw_recs, user_id, user_completed_train
            )

    hybrid_elapsed = time.perf_counter() - hybrid_t0
    timer.phases["evaluate_users"] = hybrid_elapsed
    print(f"  timing: evaluate_users {hybrid_elapsed:.2f}s")
    if skipped_no_train_completed:
        print(f"  note: {skipped_no_train_completed} users had no Completed train interactions (empty hybrid recs)")

    assert_rec_lists_complete(
        rec_lists,
        evaluated_user_ids,
        [s.label for s in specs],
    )

    elapsed = timer.total_seconds
    print(f"\nAll experiments finished in {elapsed:.1f}s")

    summary_rows: List[dict] = []
    per_user_by_experiment: Dict[str, List[dict]] = {}
    models_payload: Dict[str, Any] = {}
    metrics_t0 = time.perf_counter()

    for spec in specs:
        lists = {uid: rec_lists[spec.label].get(uid, []) for uid in evaluated_user_ids}
        rows = evaluate_rows_from_recommendations(
            user_ids=evaluated_user_ids,
            recommended_lists=lists,
            relevant_by_user=relevant_by_user,
            train_completed_count_by_user=train_completed_count_by_user,
            k=k,
        )
        per_user_by_experiment[spec.label] = rows

        quality = evaluate_quality_experiment(rows, k, spec.label)
        coverage = evaluate_coverage_diversity_novelty(
            rows=rows,
            recommended_lists=lists,
            resource_by_id=resource_by_id,
            interaction_counts=interaction_counts,
            total_interactions=total_interactions,
            catalog_size=catalog_size,
            k=k,
            label=spec.label,
        )

        gap_rows: List[float] = []
        band_rows: List[float] = []
        for user_id in evaluated_user_ids:
            _, suggested = user_context_cache.get(
                user_id,
                build_user_context(user_by_id.get(user_id, {"userId": user_id, "level": 2})),
            )
            gap, band_rate = difficulty_metrics_at_k(
                lists[user_id],
                resource_by_id,
                suggested,
                k,
                acceptable_band=args.difficulty_band,
            )
            gap_rows.append(gap)
            band_rows.append(band_rate)

        avg_gap = sum(gap_rows) / len(gap_rows) if gap_rows else 0.0
        avg_band = sum(band_rows) / len(band_rows) if band_rows else 0.0

        summary_rows.append(
            merge_summary_row(spec.label, quality, coverage, avg_gap, avg_band, k)
        )
        models_payload[spec.label] = {
            "quality": quality,
            "coverageDiversityNovelty": coverage,
            f"avgDifficultyGap@{k}": avg_gap,
            f"difficultyInBandRate@{k}": avg_band,
        }

    timer.phases["compute_metrics"] = time.perf_counter() - metrics_t0
    print(f"  timing: compute_metrics {timer.phases['compute_metrics']:.2f}s")

    summary_json = {
        "experimentConfig": experiment_config,
        "k": k,
        "summaryRows": summary_rows,
        "models": models_payload,
        "elapsedSeconds": elapsed,
        "phaseTimingsSeconds": timer.phases,
    }
    (out_dir / "evaluation_summary.json").write_text(
        json.dumps(summary_json, indent=2),
        encoding="utf-8",
    )

    pk, rk, nk, hk = f"precision@{k}", f"recall@{k}", f"ndcg@{k}", f"hitRate@{k}"
    csv_fields = [
        "experiment",
        "evaluatedUsers",
        pk,
        rk,
        nk,
        hk,
        "catalogCoverage",
        "diversity",
        "novelty",
        f"avgDifficultyGap@{k}",
        f"difficultyInBandRate@{k}",
    ]
    with (out_dir / "evaluation_summary.csv").open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=csv_fields)
        writer.writeheader()
        writer.writerows(summary_rows)

    per_user_fields = [
        "experiment",
        "userId",
        "relevantCount",
        "recommendedCount",
        "trainCompletedCount",
        pk,
        rk,
        hk,
        nk,
    ]
    merged_per_user: List[dict] = []
    for spec in specs:
        for row in per_user_by_experiment[spec.label]:
            item = dict(row)
            item["experiment"] = spec.label
            merged_per_user.append(item)

    with (out_dir / "evaluation_per_user.csv").open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=per_user_fields)
        writer.writeheader()
        writer.writerows(merged_per_user)

    write_latex_table(summary_rows, k, out_dir / "evaluation_tables.tex")

    plots_t0 = time.perf_counter()
    write_plots(summary_rows, k, figures_dir)
    timer.phases["write_plots"] = time.perf_counter() - plots_t0
    print(f"  timing: write_plots {timer.phases['write_plots']:.2f}s")

    print_summary_table(summary_rows, k)
    print(f"Saved evaluated users: {evaluated_users_path}")
    print(f"Saved summary: {out_dir / 'evaluation_summary.csv'}")
    print(f"Saved figures: {figures_dir}")


if __name__ == "__main__":
    main()
