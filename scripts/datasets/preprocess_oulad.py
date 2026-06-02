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


COURSE_WEEK_SCALE = 30

_CONTENT_TYPE_BY_ACTIVITY: dict[str, str] = {
    "oucontent": "Video",
    "ouelluminate": "Video",
    "homepage": "Article",
    "resource": "Article",
    "url": "Article",
    "page": "Article",
    "subpage": "Article",
    "sharedsubpage": "Article",
    "folder": "Article",
    "forumng": "Article",
    "ouwiki": "Article",
    "glossary": "Article",
    "oucollaborate": "Article",
    "dataplus": "Article",
    "htmlactivity": "Article",
    "dualpane": "Article",
    "quiz": "Quiz",
    "externalquiz": "Quiz",
    "questionnaire": "Quiz",
    "repeatactivity": "Quiz",
}

_DIFFICULTY_BY_ACTIVITY: dict[str, int] = {
    "homepage": 1,
    "url": 1,
    "page": 1,
    "folder": 1,
    "oucontent": 2,
    "ouelluminate": 2,
    "resource": 2,
    "subpage": 2,
    "sharedsubpage": 2,
    "glossary": 3,
    "forumng": 3,
    "ouwiki": 3,
    "htmlactivity": 3,
    "oucollaborate": 3,
    "dualpane": 4,
    "quiz": 4,
    "externalquiz": 4,
    "questionnaire": 4,
    "repeatactivity": 4,
    "dataplus": 5,
}

_ACTIVITY_LABELS: dict[str, str] = {
    "forumng": "Discussion forum",
    "oucontent": "Course study material",
    "homepage": "Course homepage",
    "resource": "Additional learning resource",
    "url": "External link",
    "page": "Course page",
    "subpage": "Topic page",
    "sharedsubpage": "Shared topic page",
    "folder": "Resource folder",
    "quiz": "Practice quiz",
    "externalquiz": "External quiz",
    "questionnaire": "Questionnaire",
    "repeatactivity": "Repeat activity",
    "glossary": "Glossary activity",
    "dataplus": "Data activity",
    "dualpane": "Interactive activity",
    "htmlactivity": "HTML learning activity",
    "oucollaborate": "Collaborative activity",
    "ouwiki": "Course wiki",
    "ouelluminate": "Live session recording",
}


def content_type_from_activity(activity_type: str) -> str:
    activity = (activity_type or "").strip().lower()
    if "quiz" in activity or "assessment" in activity:
        return "Quiz"
    if "video" in activity:
        return "Video"
    return _CONTENT_TYPE_BY_ACTIVITY.get(activity, "Article")


def difficulty_from_activity(activity_type: str) -> int:
    activity = (activity_type or "").strip().lower()
    return _DIFFICULTY_BY_ACTIVITY.get(activity, 3)


def activity_label(activity_type: str) -> str:
    activity = (activity_type or "").strip().lower()
    return _ACTIVITY_LABELS.get(activity, activity_type.strip().title() if activity_type else "Learning activity")


def week_label(week_from: object, week_to: object) -> str:
    start = safe_int(week_from, 0)
    end = safe_int(week_to, 0)
    if start <= 0 and end <= 0:
        return ""
    if start > 0 and end > 0 and start != end:
        return f"Weeks {start}-{end}"
    week = start if start > 0 else end
    return f"Week {week}"


def normalize_metadata_string(value: object) -> str:
    if value is None:
        return ""
    try:
        if pd.isna(value):
            return ""
    except Exception:
        pass
    return " ".join(str(value).split()).strip()


def build_resource_metadata_fields(
    *,
    code_module: object,
    code_presentation: object,
    activity_type: object,
    week_from: object,
    week_to: object,
) -> dict[str, str]:
    """
    Explicit structured metadata for semantic experiments.

    Only non-empty values are returned. ``topic`` remains the module code for
    backward compatibility; ``codeModule`` duplicates it for clarity.
    """
    module = normalize_metadata_string(code_module)
    presentation = normalize_metadata_string(code_presentation)
    raw_activity = normalize_metadata_string(activity_type).lower()
    label = activity_label(str(activity_type or ""))
    weeks = week_label(week_from, week_to)

    fields: dict[str, str] = {}
    if module:
        fields["codeModule"] = module
    if presentation:
        fields["presentation"] = presentation
    if raw_activity:
        fields["activityType"] = raw_activity
    if label:
        fields["activityLabel"] = label
    if weeks:
        fields["week"] = weeks
    return fields


def load_presentation_lengths(raw_dir: Path) -> Dict[Tuple[str, str], int]:
    courses = pd.read_csv(raw_dir / "courses.csv")
    lengths: Dict[Tuple[str, str], int] = {}
    for row in courses.itertuples(index=False):
        length = safe_int(getattr(row, "module_presentation_length", 0), 0)
        if length > 0:
            lengths[(str(row.code_module), str(row.code_presentation))] = length
    return lengths


def explicit_week_number(week_from: object, week_to: object) -> int:
    """Return a single positive week index from vle week_from/week_to, else 0."""
    start = safe_int(week_from, 0)
    end = safe_int(week_to, 0)
    if start > 0 and end > 0:
        return int(round((start + end) / 2))
    if start > 0:
        return start
    if end > 0:
        return end
    return 0


def infer_week_number(typical_day_offset: int, presentation_length_days: int) -> int:
    """
    Map click-weighted day offset to a 1..30 week index.

    Heuristic: ``week = round(typicalDayOffset / (presentationLengthDays / 30))``,
    capped to [1, 30]. Matches OULAD's ~30-week presentation structure.
    """
    if presentation_length_days <= 0 or typical_day_offset < 0:
        return 0
    day_per_week = presentation_length_days / COURSE_WEEK_SCALE
    if day_per_week <= 0:
        return 0
    return max(1, min(COURSE_WEEK_SCALE, int(round(typical_day_offset / day_per_week))))


def course_phase_from_week_number(week_num: int) -> str:
    """Deterministic course phase from a 1..30 week index (equal thirds)."""
    if week_num <= 0:
        return ""
    if week_num <= 10:
        return "Early"
    if week_num <= 20:
        return "Mid-course"
    return "Late"


def build_engagement_and_temporal_fields(
    *,
    code_module: str,
    code_presentation: str,
    stat: "ResourceStats",
    presentation_lengths: Dict[Tuple[str, str], int],
    explicit_week_num: int,
) -> dict:
    """Optional engagement/temporal metadata derived from studentVle aggregates."""
    fields: dict = {}
    pres_len = presentation_lengths.get((code_module, code_presentation), 0)
    if pres_len > 0:
        fields["presentationLengthDays"] = pres_len

    if stat.total_clicks > 0:
        fields["totalClicks"] = stat.total_clicks
    if stat.count_pairs > 0:
        fields["uniqueLearners"] = stat.count_pairs
    if stat.min_date is not None:
        fields["activeFromDay"] = int(stat.min_date)
    if stat.max_date is not None:
        fields["activeToDay"] = int(stat.max_date)

    typical = 0
    if stat.total_clicks > 0:
        typical = int(round(stat.weighted_day_sum / stat.total_clicks))
        fields["typicalDayOffset"] = typical

    week_num = explicit_week_num
    if week_num <= 0 and typical >= 0 and pres_len > 0 and stat.total_clicks > 0:
        week_num = infer_week_number(typical, pres_len)
        if week_num > 0:
            fields["weekInferred"] = f"Week {week_num}"

    if week_num > 0:
        phase = course_phase_from_week_number(week_num)
        if phase:
            fields["coursePhase"] = phase

    return fields


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
    min_date: int | None = None
    max_date: int | None = None
    weighted_day_sum: float = 0.0


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
        chunk["weighted_day"] = chunk["date"].fillna(0) * chunk["sum_click"]

        grouped = (
            chunk.groupby(["code_module", "code_presentation", "id_student", "id_site"], as_index=False)
            .agg(
                total_clicks=("sum_click", "sum"),
                first_date=("date", "min"),
                last_date=("date", "max"),
                weighted_day=("weighted_day", "sum"),
            )
        )
        agg_parts.append(grouped)

        site_grouped = (
            grouped.groupby(["code_module", "code_presentation", "id_site"], as_index=False)
            .agg(
                total_clicks=("total_clicks", "sum"),
                count_pairs=("id_student", "count"),
                min_date=("first_date", "min"),
                max_date=("last_date", "max"),
                weighted_day_sum=("weighted_day", "sum"),
            )
        )
        for row in site_grouped.itertuples(index=False):
            key = (row.code_module, row.code_presentation, int(row.id_site))
            st = stats.get(key, ResourceStats())
            st.total_clicks += int(row.total_clicks)
            st.count_pairs += int(row.count_pairs)
            st.weighted_day_sum += float(row.weighted_day_sum)
            row_min = safe_int(row.min_date, 0)
            row_max = safe_int(row.max_date, 0)
            if st.min_date is None or row_min < st.min_date:
                st.min_date = row_min
            if st.max_date is None or row_max > st.max_date:
                st.max_date = row_max
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
    raw_dir: Path,
    stats: Dict[Tuple[str, str, int], ResourceStats],
    modules: Set[str] | None = None,
    presentation_lengths: Dict[Tuple[str, str], int] | None = None,
) -> List[dict]:
    vle = pd.read_csv(raw_dir / "vle.csv")
    if modules:
        vle = vle[vle["code_module"].isin(modules)]
    if presentation_lengths is None:
        presentation_lengths = load_presentation_lengths(raw_dir)
    resources: List[dict] = []

    for row in vle.itertuples(index=False):
        key = (row.code_module, row.code_presentation, int(row.id_site))
        stat = stats.get(key, ResourceStats())
        avg_clicks = stat.total_clicks / stat.count_pairs if stat.count_pairs else 0.0
        estimated_duration = int(max(5, min(180, round(avg_clicks * 0.5))))

        label = activity_label(str(row.activity_type))
        week_from = getattr(row, "week_from", 0)
        week_to = getattr(row, "week_to", 0)
        weeks = week_label(week_from, week_to)
        explicit_week_num = explicit_week_number(week_from, week_to)
        resource_code = int(row.id_site)
        if weeks:
            title = f"{weeks} {label} - Module {row.code_module}"
        else:
            title = f"{label} - Module {row.code_module} #{resource_code}"
        description = (
            f"{label} from the OULAD virtual learning environment. "
            f"Module: {row.code_module}, presentation: {row.code_presentation}, "
            f"activity type: {row.activity_type}, resource id: {resource_code}."
        )
        if weeks:
            description += f" The activity is associated with {weeks.lower()} of the course timeline."

        resource = {
            "id": stable_resource_id(row.code_module, row.code_presentation, row.id_site),
            "title": title[:200],
            "description": description[:1000],
            "topic": str(row.code_module)[:100],
            "difficulty": difficulty_from_activity(str(row.activity_type)),
            "estimatedDurationMinutes": estimated_duration,
            "contentType": content_type_from_activity(str(row.activity_type)),
        }
        resource.update(
            build_resource_metadata_fields(
                code_module=row.code_module,
                code_presentation=row.code_presentation,
                activity_type=row.activity_type,
                week_from=week_from,
                week_to=week_to,
            )
        )
        resource.update(
            build_engagement_and_temporal_fields(
                code_module=str(row.code_module),
                code_presentation=str(row.code_presentation),
                stat=stat,
                presentation_lengths=presentation_lengths,
                explicit_week_num=explicit_week_num,
            )
        )
        resources.append(resource)

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

    print(f"[3/5] Building resources from {raw_dir / 'vle.csv'} + {raw_dir / 'courses.csv'}")
    presentation_lengths = load_presentation_lengths(raw_dir)
    resources = build_resources(
        raw_dir,
        resource_stats,
        modules=modules,
        presentation_lengths=presentation_lengths,
    )
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
