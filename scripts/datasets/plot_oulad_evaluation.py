#!/usr/bin/env python3
"""
Build figures and tables from OULAD offline `evaluation_report.json`
(produced by `evaluate_oulad_offline.py`)

Novelty/coverage metrics in the report assume Completed train interaction
counts

Outputs:
  - figures/ranking_metrics_comparison.png
  - figures/beyond_accuracy_comparison.png
  - figures/difficulty_ablation_delta.png
  - figures/cold_start_comparison.png
  - evaluation_tables.tex
"""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
from typing import Any, Dict, List, Sequence, Tuple

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np


ROOT_DIR = Path(__file__).resolve().parents[2]
DEFAULT_REPORT = ROOT_DIR / "datasets" / "oulad" / "processed" / "evaluation_report.json"

MODEL_ORDER: Tuple[str, ...] = (
    "popularity_baseline",
    "hybrid_no_difficulty",
    "hybrid_full",
)

MODEL_DISPLAY: Dict[str, str] = {
    "popularity_baseline": "Popularity",
    "hybrid_no_difficulty": "Hybrid (no difficulty)",
    "hybrid_full": "Hybrid (full)",
}


def load_report(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as f:
        data = json.load(f)
    if not isinstance(data, dict) or "summary" not in data:
        raise ValueError(f"Invalid report JSON (missing top-level 'summary'): {path}")
    summary = data["summary"]
    if "models" not in summary:
        raise ValueError(
            f"Report at {path} has no 'summary.models' block. "
            "Re-run scripts/datasets/evaluate_oulad_offline.py with the multi-strategy format."
        )
    return data


def get_k(summary: dict[str, Any]) -> int:
    return int(summary.get("k", 10))


def models_in_report(summary: dict[str, Any]) -> List[str]:
    m = summary.get("models") or {}
    return [name for name in MODEL_ORDER if name in m]


def _quality(summary: dict[str, Any], model: str) -> dict[str, Any]:
    return (summary.get("models") or {}).get(model, {}).get("quality") or {}


def _coverage(summary: dict[str, Any], model: str) -> dict[str, Any]:
    return (summary.get("models") or {}).get(model, {}).get("coverageDiversityNovelty") or {}


def _cold(summary: dict[str, Any], model: str) -> dict[str, Any]:
    return (summary.get("models") or {}).get(model, {}).get("coldStart") or {}


def apply_thesis_rc() -> None:
    plt.rcParams.update(
        {
            "figure.dpi": 120,
            "savefig.dpi": 300,
            "font.size": 11,
            "axes.titlesize": 12,
            "axes.labelsize": 11,
            "legend.fontsize": 9,
            "xtick.labelsize": 9,
            "ytick.labelsize": 9,
            "axes.grid": True,
            "grid.alpha": 0.25,
            "axes.axisbelow": True,
            "figure.facecolor": "white",
            "axes.facecolor": "white",
            "pdf.fonttype": 42,
            "ps.fonttype": 42,
        }
    )


def grouped_bar_chart(
    ax: plt.Axes,
    x_labels: Sequence[str],
    series: Dict[str, Sequence[float]],
    *,
    ylabel: str,
    title: str,
    ylim: Tuple[float, float] | None = None,
) -> None:
    """series: display_name -> values aligned with x_labels."""
    names = list(series.keys())
    n_x = len(x_labels)
    n_s = max(1, len(names))
    width = 0.8 / n_s
    x = np.arange(n_x, dtype=float)
    colors = ["#4C72B0", "#DD8452", "#55A868", "#C44E52", "#8172B3"][:n_s]

    for i, name in enumerate(names):
        vals = list(series[name])
        if len(vals) != n_x:
            raise ValueError(f"Series {name!r} length {len(vals)} != n_x {n_x}")
        offset = (i - (n_s - 1) / 2.0) * width
        ax.bar(x + offset, vals, width=width * 0.92, label=name, color=colors[i % len(colors)], edgecolor="0.2", linewidth=0.4)

    ax.set_xticks(x)
    ax.set_xticklabels(list(x_labels))
    ax.set_ylabel(ylabel)
    ax.set_title(title)
    ax.legend(loc="upper right", frameon=True, fancybox=False, edgecolor="0.5")
    if ylim is not None:
        ax.set_ylim(*ylim)
    ax.margins(x=0.02)


def plot_ranking_metrics(
    summary: dict[str, Any],
    models: List[str],
    k: int,
    out_path: Path,
) -> None:
    pk, rk, nk, hk = f"precision@{k}", f"recall@{k}", f"ndcg@{k}", f"hitRate@{k}"
    x_labels = [
        f"Precision@{k}",
        f"Recall@{k}",
        f"NDCG@{k}",
        f"Hit rate@{k}",
    ]
    keys = [pk, rk, nk, hk]

    series: Dict[str, List[float]] = {}
    for m in models:
        q = _quality(summary, m)
        series[MODEL_DISPLAY.get(m, m)] = [float(q.get(key, 0.0) or 0.0) for key in keys]

    fig, ax = plt.subplots(figsize=(9.5, 4.8))
    grouped_bar_chart(
        ax,
        x_labels,
        series,
        ylabel="Score (mean)",
        title=f"Ranking metrics @ {k} (OULAD offline evaluation)",
        ylim=(0.0, 1.05),
    )
    fig.tight_layout()
    fig.savefig(out_path, bbox_inches="tight")
    plt.close(fig)


def plot_beyond_accuracy(
    summary: dict[str, Any],
    models: List[str],
    out_path: Path,
) -> None:
    x_labels = ["Coverage", "Diversity", "Novelty"]
    series: Dict[str, List[float]] = {}
    for m in models:
        c = _coverage(summary, m)
        series[MODEL_DISPLAY.get(m, m)] = [
            float(c.get("catalogCoverage", 0.0) or 0.0),
            float(c.get("diversity", 0.0) or 0.0),
            float(c.get("novelty", 0.0) or 0.0),
        ]

    fig, ax = plt.subplots(figsize=(8.5, 4.8))
    grouped_bar_chart(
        ax,
        x_labels,
        series,
        ylabel="Score (aggregate / mean)",
        title="Beyond-accuracy metrics (OULAD offline evaluation)",
        ylim=(0.0, 1.05),
    )
    fig.tight_layout()
    fig.savefig(out_path, bbox_inches="tight")
    plt.close(fig)


def plot_ablation_delta(
    summary: dict[str, Any],
    k: int,
    out_path: Path,
) -> None:
    deltas = summary.get("comparisonDeltas") or {}
    key = "hybrid_full_minus_hybrid_no_difficulty"
    if key not in deltas:
        raise ValueError(
            f"Missing comparison delta {key!r} in report. "
            "Re-run evaluation with both hybrid_full and hybrid_no_difficulty."
        )
    d = deltas[key]
    pk, rk, nk, hk = f"precision@{k}", f"recall@{k}", f"ndcg@{k}", f"hitRate@{k}"
    labels = [
        f"Δ Precision@{k}",
        f"Δ Recall@{k}",
        f"Δ NDCG@{k}",
        f"Δ Hit rate@{k}",
        "Δ Coverage",
        "Δ Diversity",
        "Δ Novelty",
    ]
    keys = [pk, rk, nk, hk, "catalogCoverage", "diversity", "novelty"]
    values = [float(d.get(x, 0.0) or 0.0) for x in keys]
    colors = ["#4C72B0" if v >= 0 else "#C44E52" for v in values]

    fig, ax = plt.subplots(figsize=(9.5, 4.5))
    x = np.arange(len(labels))
    ax.bar(x, values, color=colors, edgecolor="0.2", linewidth=0.45)
    ax.axhline(0.0, color="0.35", linewidth=0.8)
    ax.set_xticks(x)
    ax.set_xticklabels(labels, rotation=18, ha="right")
    ax.set_ylabel("hybrid_full − hybrid_no_difficulty")
    ax.set_title("Difficulty ablation: full hybrid minus hybrid without difficulty term")
    ymax = max(0.05, max(abs(v) for v in values) * 1.15) if values else 0.1
    ax.set_ylim(-ymax, ymax)
    fig.tight_layout()
    fig.savefig(out_path, bbox_inches="tight")
    plt.close(fig)


def plot_cold_start(
    summary: dict[str, Any],
    models: List[str],
    k: int,
    out_path: Path,
) -> None:
    buckets = ["cold_0_2", "warm_3_9", "hot_10_plus"]
    bucket_labels = ["Cold (0–2 train completed)", "Warm (3–9)", "Hot (10+)"]
    ndcg_key = f"ndcg@{k}"
    hk_key = f"hitRate@{k}"

    fig, (ax0, ax1) = plt.subplots(2, 1, figsize=(9.5, 7.2), sharex=True)

    for ax, metric_key, title_suffix, ylim in (
        (ax0, ndcg_key, f"NDCG@{k}", (0.0, 1.05)),
        (ax1, hk_key, f"Hit rate@{k}", (0.0, 1.05)),
    ):
        series: Dict[str, List[float]] = {}
        for m in models:
            cold = _cold(summary, m)
            bmap = cold.get("buckets") or {}
            vals: List[float] = []
            for b in buckets:
                block = bmap.get(b) or {}
                vals.append(float(block.get(metric_key, 0.0) or 0.0))
            series[MODEL_DISPLAY.get(m, m)] = vals
        grouped_bar_chart(
            ax,
            bucket_labels,
            series,
            ylabel="Score (mean)",
            title=f"Cold-start buckets — {title_suffix}",
            ylim=ylim,
        )

    fig.suptitle("Cold-start comparison across recommenders", fontsize=13, y=1.02)
    fig.tight_layout()
    fig.savefig(out_path, bbox_inches="tight")
    plt.close(fig)


def _tex_escape(s: str) -> str:
    return (
        s.replace("\\", "\\textbackslash{}")
        .replace("&", "\\&")
        .replace("%", "\\%")
        .replace("#", "\\#")
        .replace("_", "\\_")
    )


def _fmt(v: Any, decimals: int = 4) -> str:
    if v is None or (isinstance(v, float) and (math.isnan(v) or math.isinf(v))):
        return "---"
    if isinstance(v, (int, float)):
        return f"{float(v):.{decimals}f}"
    return _tex_escape(str(v))


def write_evaluation_tables_tex(
    data: dict[str, Any],
    out_path: Path,
) -> None:
    summary = data["summary"]
    k = get_k(summary)
    models = models_in_report(summary)
    pk, rk, nk, hk = f"precision@{k}", f"recall@{k}", f"ndcg@{k}", f"hitRate@{k}"

    lines: List[str] = [
        "% Auto-generated by scripts/datasets/plot_oulad_evaluation.py",
        "% Include in your thesis with: \\input{evaluation_tables}",
        "% Requires: \\usepackage{booktabs}",
        "",
        "\\providecommand{\\EvalTableK}{" + str(k) + "}",
        "",
    ]

    # --- Main comparison ---
    lines += [
        "\\begin{table}[htbp]",
        "\\centering",
        f"\\caption{{Main offline metrics (mean) at $k={k}$.}}",
        "\\label{tab:oulad-main-metrics}",
        "\\begin{tabular}{lrrrrrrr}",
        "\\toprule",
        f"Model & P@\\EvalTableK & R@\\EvalTableK & NDCG@\\EvalTableK & HR@\\EvalTableK & Cov. & Div. & Nov. \\\\",
        "\\midrule",
    ]
    for m in models:
        q = _quality(summary, m)
        c = _coverage(summary, m)
        label = _tex_escape(MODEL_DISPLAY.get(m, m))
        lines.append(
            " & ".join(
                [
                    label,
                    _fmt(q.get(pk)),
                    _fmt(q.get(rk)),
                    _fmt(q.get(nk)),
                    _fmt(q.get(hk)),
                    _fmt(c.get("catalogCoverage")),
                    _fmt(c.get("diversity")),
                    _fmt(c.get("novelty")),
                ]
            )
            + " \\\\"
        )
    lines += ["\\bottomrule", "\\end{tabular}", "\\end{table}", ""]

    # --- Ablation delta ---
    delta_key = "hybrid_full_minus_hybrid_no_difficulty"
    deltas = summary.get("comparisonDeltas") or {}
    lines += [
        "\\begin{table}[htbp]",
        "\\centering",
        "\\caption{Difficulty ablation: hybrid with difficulty minus hybrid without (aggregate deltas).}",
        "\\label{tab:oulad-ablation-delta}",
        "\\begin{tabular}{lrrrrrrr}",
        "\\toprule",
        f"Contrast & $\\Delta$P@\\EvalTableK & $\\Delta$R@\\EvalTableK & $\\Delta$NDCG@\\EvalTableK & $\\Delta$HR@\\EvalTableK & $\\Delta$Cov. & $\\Delta$Div. & $\\Delta$Nov. \\\\",
        "\\midrule",
    ]
    if delta_key in deltas:
        d = deltas[delta_key]
        lines.append(
            " & ".join(
                [
                    _tex_escape("hybrid_full - hybrid_no_difficulty"),
                    _fmt(d.get(pk)),
                    _fmt(d.get(rk)),
                    _fmt(d.get(nk)),
                    _fmt(d.get(hk)),
                    _fmt(d.get("catalogCoverage")),
                    _fmt(d.get("diversity")),
                    _fmt(d.get("novelty")),
                ]
            )
            + " \\\\"
        )
    else:
        lines.append("\\multicolumn{8}{l}{\\textit{(Run evaluation with both hybrid variants to populate this row.)}} \\\\")
    lines += ["\\bottomrule", "\\end{tabular}", "\\end{table}", ""]

    # --- Configuration ---
    cfg = summary.get("experimentConfig") or {}
    dst = summary.get("datasetStatistics") or {}
    lines += [
        "\\begin{table}[htbp]",
        "\\centering",
        "\\caption{Experimental configuration and dataset statistics (OULAD offline evaluation).}",
        "\\label{tab:oulad-eval-config}",
        "\\begin{tabular}{ll}",
        "\\toprule",
        "Field & Value \\\\",
        "\\midrule",
    ]
    rows_kv: List[Tuple[str, Any]] = [
        ("Evaluated variants", ", ".join(models)),
        ("$k$", k),
        ("Processed directory", cfg.get("processedDir", "")),
        ("Output directory", cfg.get("outputDir", "")),
        ("Backend URL", cfg.get("backendUrl", "")),
        ("AI service URL", cfg.get("aiUrl", "")),
        ("Min test completed", cfg.get("minTestCompleted", "")),
        ("Max users", cfg.get("maxUsers", "")),
        ("Sort users by test size", cfg.get("sortUsersByTestSize", "")),
        ("Timeout (s)", cfg.get("timeoutSeconds", "")),
        ("---", "---"),
        ("Catalog resources", dst.get("catalogResourceCount", "")),
        ("Train interactions", dst.get("trainInteractionCount", "")),
        ("Train completed interactions", dst.get("trainCompletedInteractionCount", "")),
        ("Test interactions", dst.get("testInteractionCount", "")),
        ("Test completed interactions", dst.get("testCompletedInteractionCount", "")),
        ("Test users (relevant completed)", dst.get("testUsersWithRelevantCompleted", "")),
        ("Candidate users (after filters)", dst.get("candidateUsersAfterFilters", "")),
        ("Evaluated users (all strategies)", dst.get("evaluatedUsersAllStrategies", "")),
        ("Train global interaction events", dst.get("trainGlobalInteractionEventCount", "")),
    ]
    for label, val in rows_kv:
        lines.append(f"{_tex_escape(str(label))} & {_tex_escape(str(val))} \\\\")
    lines += ["\\bottomrule", "\\end{tabular}", "\\end{table}", ""]

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Plots and LaTeX tables from OULAD evaluation_report.json.")
    parser.add_argument(
        "--report",
        type=Path,
        default=DEFAULT_REPORT,
        help="Path to evaluation_report.json",
    )
    parser.add_argument(
        "--figures-dir",
        type=Path,
        default=None,
        help="Directory for PNG figures (default: <report_parent>/figures)",
    )
    parser.add_argument(
        "--tables-out",
        type=Path,
        default=None,
        help="Path for evaluation_tables.tex (default: <report_parent>/evaluation_tables.tex)",
    )
    args = parser.parse_args()

    report_path = args.report.resolve()
    if not report_path.is_file():
        raise SystemExit(f"Report not found: {report_path}")

    data = load_report(report_path)
    summary = data["summary"]
    k = get_k(summary)
    models = models_in_report(summary)
    if len(models) < 2:
        raise SystemExit("Need at least two models in the report to build comparison plots.")

    parent = report_path.parent
    figures_dir = (args.figures_dir or (parent / "figures")).resolve()
    tables_out = (args.tables_out or (parent / "evaluation_tables.tex")).resolve()
    figures_dir.mkdir(parents=True, exist_ok=True)

    apply_thesis_rc()

    paths: List[Path] = []

    p1 = figures_dir / "ranking_metrics_comparison.png"
    plot_ranking_metrics(summary, models, k, p1)
    paths.append(p1)

    p2 = figures_dir / "beyond_accuracy_comparison.png"
    plot_beyond_accuracy(summary, models, p2)
    paths.append(p2)

    p3 = figures_dir / "difficulty_ablation_delta.png"
    try:
        plot_ablation_delta(summary, k, p3)
        paths.append(p3)
    except ValueError as e:
        print(f"Warning: skip ablation plot: {e}")

    p4 = figures_dir / "cold_start_comparison.png"
    plot_cold_start(summary, models, k, p4)
    paths.append(p4)

    write_evaluation_tables_tex(data, tables_out)
    paths.append(tables_out)

    print("Generated:")
    for p in paths:
        print(f"  {p}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
