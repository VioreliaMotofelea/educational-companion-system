#!/usr/bin/env python3
"""
Preprocess OULAD raw CSV files into backend/AI-service friendly JSON files.

Outputs:
  - datasets/oulad/processed/resources.json
  - datasets/oulad/processed/users.json
  - datasets/oulad/processed/interactions_train.json
  - datasets/oulad/processed/interactions_test.json
"""

from __future__ import annotations

import argparse
import json
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Set, Tuple

try:
    import pandas as pd
except ModuleNotFoundError as exc:
    raise SystemExit(
        "Missing dependency: pandas. Install dependencies first, e.g. "
        "`pip install -r ai-service/requirements.txt`."
    ) from exc


ROOT_DIR = Path(__file__).resolve().parents[2]
DEFAULT_RAW_DIR = ROOT_DIR / "datasets" / "oulad" / "raw"
DEFAULT_PROCESSED_DIR = ROOT_DIR / "datasets" / "oulad" / "processed"


def stable_user_id(code_module: str, code_presentation: str, id_student: int) -> str:
    return f"oulad-{code_module}-{code_presentation}-{int(id_student)}"


def stable_resource_id(code_module: str, code_presentation: str, id_site: int) -> str:
    raw_id = f"oulad:{code_module}:{code_presentation}:{int(id_site)}"
    # Backend expects Guid-like IDs for resources; use deterministic UUIDv5.
    return str(uuid.uuid5(uuid.NAMESPACE_URL, raw_id))


def safe_int(value: object, default: int = 0) -> int:
    try:
        if pd.isna(value):
            return default
        return int(float(value))
    except Exception:
        return default


def normalize_score_to_rating(score: float | None) -> int | None:
    if score is None:
        return None
    if pd.isna(score):
        return None
    # OULAD score is mostly 0..100. Map to backend rating 1..5.
    value = max(0.0, min(100.0, float(score)))
    return max(1, min(5, int(round((value / 100.0) * 4 + 1))))


def content_type_from_activity(activity_type: str) -> str:
    activity = (activity_type or "").strip().lower()
    if "quiz" in activity or "assessment" in activity:
        return "Quiz"
    if "video" in activity or "oucontent" in activity:
        return "Video"
    return "Article"


def difficulty_from_activity(activity_type: str) -> int:
    activity = (activity_type or "").strip().lower()
    mapping = {
        "homepage": 1,
        "oucontent": 2,
        "resource": 2,
        "subpage": 2,
        "forumng": 3,
        "glossary": 3,
        "externalquiz": 4,
        "quiz": 4,
        "dualpane": 4,
        "dataplus": 5,
        "htmlactivity": 3,
    }
    return mapping.get(activity, 3)


def load_assessment_course_scores(raw_dir: Path) -> Dict[Tuple[str, str, int], float]:
    assessments = pd.read_csv(raw_dir / "assessments.csv")
    student_assessment = pd.read_csv(raw_dir / "studentAssessment.csv")
    student_assessment["score"] = pd.to_numeric(student_assessment["score"], errors="coerce")

    merged = student_assessment.merge(
        assessments[["id_assessment", "code_module", "code_presentation"]],
        on="id_assessment",
        how="left",
    )
    merged = merged.dropna(subset=["code_module", "code_presentation", "id_student", "score"])
    grouped = (
        merged.groupby(["code_module", "code_presentation", "id_student"], as_index=False)["score"]
        .mean()
        .rename(columns={"score": "avg_score"})
    )

    result: Dict[Tuple[str, str, int], float] = {}
    for row in grouped.itertuples(index=False):
        result[(row.code_module, row.code_presentation, int(row.id_student))] = float(row.avg_score)
    return result


def build_users(raw_dir: Path, modules: Set[str] | None = None, max_users: int = 0) -> List[dict]:
    student_info = pd.read_csv(raw_dir / "studentInfo.csv")
    student_info = student_info.drop_duplicates(subset=["code_module", "code_presentation", "id_student"])
    if modules:
        student_info = student_info[student_info["code_module"].isin(modules)]

    education_to_level = {
        "No Formal quals": 1,
        "Lower Than A Level": 1,
        "A Level or Equivalent": 2,
        "HE Qualification": 3,
        "Post Graduate Qualification": 4,
    }

    users: List[dict] = []
    for row in student_info.itertuples(index=False):
        if max_users > 0 and len(users) >= max_users:
            break

        level = education_to_level.get(str(row.highest_education), 2)
        studied_credits = safe_int(getattr(row, "studied_credits", 0), 0)
        prev_attempts = safe_int(getattr(row, "num_of_prev_attempts", 0), 0)
        xp = max(0, min(99999, studied_credits * 10 + prev_attempts * 20))

        users.append(
            {
                "userId": stable_user_id(row.code_module, row.code_presentation, row.id_student),
                "level": level,
                "xp": xp,
                "dailyAvailableMinutes": 60,
            }
        )

    return users


@dataclass
class ResourceStats:
    total_clicks: int = 0
    count_pairs: int = 0


def aggregate_student_vle(
    raw_dir: Path,
    chunksize: int,
    modules: Set[str] | None = None,
    allowed_users: Set[str] | None = None,
) -> Tuple[pd.DataFrame, Dict[Tuple[str, str, int], ResourceStats]]:
    agg_parts: List[pd.DataFrame] = []
    stats: Dict[Tuple[str, str, int], ResourceStats] = {}

    use_cols = ["code_module", "code_presentation", "id_student", "id_site", "date", "sum_click"]
    for chunk in pd.read_csv(raw_dir / "studentVle.csv", usecols=use_cols, chunksize=chunksize):
        if modules:
            chunk = chunk[chunk["code_module"].isin(modules)]
        if chunk.empty:
            continue

        if allowed_users is not None:
            chunk["userId"] = (
                "oulad-"
                + chunk["code_module"].astype(str)
                + "-"
                + chunk["code_presentation"].astype(str)
                + "-"
                + chunk["id_student"].astype("Int64").astype(str)
            )
            chunk = chunk[chunk["userId"].isin(allowed_users)]
            chunk = chunk.drop(columns=["userId"])
            if chunk.empty:
                continue

        chunk["sum_click"] = pd.to_numeric(chunk["sum_click"], errors="coerce").fillna(0).astype(int)
        chunk["date"] = pd.to_numeric(chunk["date"], errors="coerce")

        grouped = (
            chunk.groupby(["code_module", "code_presentation", "id_student", "id_site"], as_index=False)
            .agg(total_clicks=("sum_click", "sum"), last_date=("date", "max"))
        )
        agg_parts.append(grouped)

        site_grouped = (
            grouped.groupby(["code_module", "code_presentation", "id_site"], as_index=False)
            .agg(total_clicks=("total_clicks", "sum"), count_pairs=("id_student", "count"))
        )
        for row in site_grouped.itertuples(index=False):
            key = (row.code_module, row.code_presentation, int(row.id_site))
            st = stats.get(key, ResourceStats())
            st.total_clicks += int(row.total_clicks)
            st.count_pairs += int(row.count_pairs)
            stats[key] = st

    if not agg_parts:
        return pd.DataFrame(), stats

    merged = pd.concat(agg_parts, ignore_index=True)
    merged = (
        merged.groupby(["code_module", "code_presentation", "id_student", "id_site"], as_index=False)
        .agg(total_clicks=("total_clicks", "sum"), last_date=("last_date", "max"))
    )
    return merged, stats


def build_resources(
    raw_dir: Path, stats: Dict[Tuple[str, str, int], ResourceStats], modules: Set[str] | None = None
) -> List[dict]:
    vle = pd.read_csv(raw_dir / "vle.csv")
    if modules:
        vle = vle[vle["code_module"].isin(modules)]
    resources: List[dict] = []

    for row in vle.itertuples(index=False):
        key = (row.code_module, row.code_presentation, int(row.id_site))
        stat = stats.get(key, ResourceStats())
        avg_clicks = stat.total_clicks / stat.count_pairs if stat.count_pairs else 0.0
        estimated_duration = int(max(5, min(180, round(avg_clicks * 0.5))))

        title = f"{row.activity_type} resource {int(row.id_site)}"
        description = (
            f"OULAD learning activity ({row.activity_type}) "
            f"for module {row.code_module}-{row.code_presentation}. "
            f"Active between weeks {safe_int(getattr(row, 'week_from', 0), 0)} and "
            f"{safe_int(getattr(row, 'week_to', 0), 0)}."
        )

        resources.append(
            {
                "id": stable_resource_id(row.code_module, row.code_presentation, row.id_site),
                "title": title[:200],
                "description": description[:1000],
                "topic": str(row.code_module)[:100],
                "difficulty": difficulty_from_activity(str(row.activity_type)),
                "estimatedDurationMinutes": estimated_duration,
                "contentType": content_type_from_activity(str(row.activity_type)),
            }
        )

    # Keep one record per deterministic id.
    dedup: Dict[str, dict] = {item["id"]: item for item in resources}
    return list(dedup.values())


def build_interactions(
    aggregated: pd.DataFrame,
    min_clicks_for_completed: int,
    test_user_percent: int,
    assessment_scores: Dict[Tuple[str, str, int], float],
) -> Tuple[List[dict], List[dict]]:
    train: List[dict] = []
    test: List[dict] = []

    if aggregated.empty:
        return train, test

    interactions_by_user: Dict[str, List[dict]] = {}

    for row in aggregated.itertuples(index=False):
        total_clicks = safe_int(row.total_clicks, 0)
        if total_clicks <= 0:
            continue

        user_id = stable_user_id(row.code_module, row.code_presentation, row.id_student)
        resource_id = stable_resource_id(row.code_module, row.code_presentation, row.id_site)
        course_key = (row.code_module, row.code_presentation, int(row.id_student))
        rating = normalize_score_to_rating(assessment_scores.get(course_key))

        # OULAD date is day offset in module timeline. Build a simple normalized timestamp.
        day_offset = safe_int(row.last_date, 0)
        day_offset = max(0, min(3650, day_offset))
        created_at = (pd.Timestamp("2013-01-01") + pd.Timedelta(days=day_offset)).isoformat() + "Z"

        interaction_type = "Completed" if total_clicks >= min_clicks_for_completed else "Viewed"
        interaction = {
            "userId": user_id,
            "learningResourceId": resource_id,
            "interactionType": interaction_type,
            "timeSpentMinutes": max(1, min(240, int(round(total_clicks * 0.5)))),
            "createdAtUtc": created_at,
            "_dayOffset": day_offset,
        }
        if rating is not None:
            interaction["rating"] = rating

        interactions_by_user.setdefault(user_id, []).append(interaction)

    for _, user_interactions in interactions_by_user.items():
        user_interactions.sort(key=lambda x: (safe_int(x.get("_dayOffset"), 0), str(x["learningResourceId"])))
        total = len(user_interactions)

        if total == 1:
            train.append(user_interactions[0])
            continue

        test_count = int(round(total * (test_user_percent / 100.0)))
        test_count = max(1, min(total - 1, test_count))
        split_index = total - test_count

        train.extend(user_interactions[:split_index])
        test.extend(user_interactions[split_index:])

    for rec in train:
        rec.pop("_dayOffset", None)
    for rec in test:
        rec.pop("_dayOffset", None)

    return train, test


def filter_known_ids(records: Iterable[dict], users: set[str], resources: set[str]) -> List[dict]:
    return [
        r
        for r in records
        if r.get("userId") in users and r.get("learningResourceId") in resources
    ]


def write_json(path: Path, payload: List[dict]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as file:
        json.dump(payload, file, ensure_ascii=False, indent=2)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Preprocess OULAD into JSON files for backend + AI service.")
    parser.add_argument("--raw-dir", type=Path, default=DEFAULT_RAW_DIR, help="Directory with OULAD raw CSV files.")
    parser.add_argument(
        "--processed-dir",
        type=Path,
        default=DEFAULT_PROCESSED_DIR,
        help="Output directory for processed JSON files.",
    )
    parser.add_argument(
        "--chunksize",
        type=int,
        default=500_000,
        help="Chunk size for streaming studentVle.csv.",
    )
    parser.add_argument(
        "--min-clicks-for-completed",
        type=int,
        default=5,
        help="Minimum aggregated clicks(user,resource) to emit a Completed interaction.",
    )
    parser.add_argument(
        "--test-user-percent",
        type=int,
        default=20,
        help="Percent of each user's latest interactions assigned to test (chronological split).",
    )
    parser.add_argument(
        "--modules",
        nargs="+",
        default=None,
        help="Optional list of OULAD module codes to keep (e.g. AAA BBB).",
    )
    parser.add_argument(
        "--max-users",
        type=int,
        default=0,
        help="Optional cap on number of users (0 means all).",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    raw_dir: Path = args.raw_dir
    processed_dir: Path = args.processed_dir

    if not raw_dir.exists():
        raise FileNotFoundError(f"Raw dir does not exist: {raw_dir}")

    modules = set(args.modules) if args.modules else None

    print(f"[1/5] Building users from {raw_dir / 'studentInfo.csv'}")
    users = build_users(raw_dir, modules=modules, max_users=max(0, args.max_users))
    user_ids = {u["userId"] for u in users}

    print(f"[2/5] Aggregating interactions from {raw_dir / 'studentVle.csv'} in chunks")
    aggregated, resource_stats = aggregate_student_vle(
        raw_dir,
        chunksize=args.chunksize,
        modules=modules,
        allowed_users=user_ids,
    )

    print(f"[3/5] Building resources from {raw_dir / 'vle.csv'}")
    resources = build_resources(raw_dir, resource_stats, modules=modules)
    resource_ids = {r["id"] for r in resources}

    print(f"[4/5] Mapping assessment scores from {raw_dir / 'studentAssessment.csv'}")
    assessment_scores = load_assessment_course_scores(raw_dir)

    print("[5/5] Creating train/test interactions")
    train, test = build_interactions(
        aggregated=aggregated,
        min_clicks_for_completed=args.min_clicks_for_completed,
        test_user_percent=max(1, min(99, args.test_user_percent)),
        assessment_scores=assessment_scores,
    )
    train = filter_known_ids(train, users=user_ids, resources=resource_ids)
    test = filter_known_ids(test, users=user_ids, resources=resource_ids)

    write_json(processed_dir / "users.json", users)
    write_json(processed_dir / "resources.json", resources)
    write_json(processed_dir / "interactions_train.json", train)
    write_json(processed_dir / "interactions_test.json", test)

    print("Done.")
    print(f"  users: {len(users)}")
    print(f"  resources: {len(resources)}")
    print(f"  interactions_train: {len(train)}")
    print(f"  interactions_test: {len(test)}")


if __name__ == "__main__":
    main()
