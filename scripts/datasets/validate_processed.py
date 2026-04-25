#!/usr/bin/env python3
"""
Validate processed OULAD JSON files before seeding.
"""

from __future__ import annotations

import argparse
import json
from collections import Counter, defaultdict
from pathlib import Path
from typing import Dict, List, Tuple


ROOT_DIR = Path(__file__).resolve().parents[2]
DEFAULT_PROCESSED_DIR = ROOT_DIR / "datasets" / "oulad" / "processed"
ALLOWED_CONTENT_TYPES = {"Article", "Video", "Quiz"}
ALLOWED_INTERACTION_TYPES = {"Viewed", "Completed", "Rated", "Skipped"}


def load_json_array(path: Path) -> List[dict]:
    if not path.exists():
        raise FileNotFoundError(f"Missing file: {path}")
    with path.open("r", encoding="utf-8") as f:
        data = json.load(f)
    if not isinstance(data, list):
        raise ValueError(f"Expected JSON array in {path}")
    return data


def require_keys(record: dict, keys: List[str], context: str, errors: List[str]) -> None:
    for key in keys:
        if key not in record:
            errors.append(f"{context}: missing required key '{key}'")


def validate_users(users: List[dict], errors: List[str], warnings: List[str]) -> None:
    seen_user_ids = set()
    for i, user in enumerate(users):
        ctx = f"users[{i}]"
        require_keys(user, ["userId", "level", "xp", "dailyAvailableMinutes"], ctx, errors)
        user_id = user.get("userId")
        if not isinstance(user_id, str) or not user_id.strip():
            errors.append(f"{ctx}: userId must be non-empty string")
        elif user_id in seen_user_ids:
            errors.append(f"{ctx}: duplicate userId '{user_id}'")
        else:
            seen_user_ids.add(user_id)

        level = user.get("level")
        if not isinstance(level, int) or not (1 <= level <= 5):
            warnings.append(f"{ctx}: level should be int in range 1..5")

        daily = user.get("dailyAvailableMinutes")
        if not isinstance(daily, int) or daily < 0:
            errors.append(f"{ctx}: dailyAvailableMinutes must be int >= 0")


def validate_resources(resources: List[dict], errors: List[str], warnings: List[str]) -> None:
    seen_resource_ids = set()
    for i, res in enumerate(resources):
        ctx = f"resources[{i}]"
        require_keys(
            res,
            ["id", "title", "topic", "difficulty", "estimatedDurationMinutes", "contentType"],
            ctx,
            errors,
        )
        resource_id = res.get("id")
        if not isinstance(resource_id, str) or not resource_id.strip():
            errors.append(f"{ctx}: id must be non-empty string (uuid)")
        elif resource_id in seen_resource_ids:
            errors.append(f"{ctx}: duplicate resource id '{resource_id}'")
        else:
            seen_resource_ids.add(resource_id)

        title = res.get("title")
        if not isinstance(title, str) or not title.strip():
            errors.append(f"{ctx}: title must be non-empty string")
        elif len(title) > 200:
            warnings.append(f"{ctx}: title longer than backend max 200")

        topic = res.get("topic")
        if not isinstance(topic, str) or not topic.strip():
            errors.append(f"{ctx}: topic must be non-empty string")
        elif len(topic) > 100:
            warnings.append(f"{ctx}: topic longer than backend max 100")

        difficulty = res.get("difficulty")
        if not isinstance(difficulty, int) or not (1 <= difficulty <= 5):
            errors.append(f"{ctx}: difficulty must be int in range 1..5")

        duration = res.get("estimatedDurationMinutes")
        if not isinstance(duration, int) or not (1 <= duration <= 9999):
            errors.append(f"{ctx}: estimatedDurationMinutes must be int in range 1..9999")

        content_type = res.get("contentType")
        if content_type not in ALLOWED_CONTENT_TYPES:
            errors.append(f"{ctx}: invalid contentType '{content_type}'")


def validate_interactions(
    train: List[dict],
    test: List[dict],
    user_ids: set[str],
    resource_ids: set[str],
    min_overlap_users: int,
    errors: List[str],
    warnings: List[str],
) -> None:
    if not train:
        warnings.append("interactions_train.json is empty")
    if not test:
        warnings.append("interactions_test.json is empty")

    def validate_split(records: List[dict], split_name: str) -> List[Tuple[str, str, str, str]]:
        keys: List[Tuple[str, str, str, str]] = []
        for i, rec in enumerate(records):
            ctx = f"{split_name}[{i}]"
            require_keys(rec, ["userId", "learningResourceId", "interactionType"], ctx, errors)
            user_id = rec.get("userId")
            resource_id = rec.get("learningResourceId")
            interaction_type = rec.get("interactionType")
            created_at = str(rec.get("createdAtUtc", ""))

            if user_id not in user_ids:
                errors.append(f"{ctx}: userId '{user_id}' not found in users.json")
            if resource_id not in resource_ids:
                errors.append(f"{ctx}: learningResourceId '{resource_id}' not found in resources.json")
            if interaction_type not in ALLOWED_INTERACTION_TYPES:
                errors.append(f"{ctx}: invalid interactionType '{interaction_type}'")

            rating = rec.get("rating")
            if rating is not None and (not isinstance(rating, int) or not (1 <= rating <= 5)):
                errors.append(f"{ctx}: rating must be int in range 1..5 if provided")

            time_spent = rec.get("timeSpentMinutes")
            if time_spent is not None and (not isinstance(time_spent, int) or not (0 <= time_spent <= 9999)):
                errors.append(f"{ctx}: timeSpentMinutes must be int in range 0..9999 if provided")

            keys.append((str(user_id), str(resource_id), str(interaction_type), created_at))
        return keys

    train_keys = validate_split(train, "train")
    test_keys = validate_split(test, "test")

    intersection = set(train_keys).intersection(set(test_keys))
    if intersection:
        errors.append(f"Found {len(intersection)} duplicate interactions present in both train and test")

    train_by_user: Dict[str, List[dict]] = defaultdict(list)
    test_by_user: Dict[str, List[dict]] = defaultdict(list)
    for rec in train:
        train_by_user[str(rec.get("userId"))].append(rec)
    for rec in test:
        test_by_user[str(rec.get("userId"))].append(rec)

    for user_id in set(train_by_user.keys()).intersection(test_by_user.keys()):
        train_dates = [str(x.get("createdAtUtc", "")) for x in train_by_user[user_id] if x.get("createdAtUtc")]
        test_dates = [str(x.get("createdAtUtc", "")) for x in test_by_user[user_id] if x.get("createdAtUtc")]
        if not train_dates or not test_dates:
            continue
        if max(train_dates) > min(test_dates):
            warnings.append(
                f"userId '{user_id}' has non-chronological split (some train interactions occur after test start)"
            )

    overlap_users = set(train_by_user.keys()).intersection(test_by_user.keys())
    if len(overlap_users) == 0:
        errors.append("No users appear in both train and test; offline evaluation quality will be poor.")
    elif len(overlap_users) < min_overlap_users:
        warnings.append(
            f"Only {len(overlap_users)} users appear in both train and test "
            f"(recommended minimum: {min_overlap_users})."
        )

    all_types = Counter([str(x.get("interactionType")) for x in train + test])
    if all_types.get("Completed", 0) == 0:
        warnings.append("No 'Completed' interactions found; recommender may not perform well")


def print_summary(users: List[dict], resources: List[dict], train: List[dict], test: List[dict]) -> None:
    print("Validation summary:")
    print(f"  users: {len(users)}")
    print(f"  resources: {len(resources)}")
    print(f"  interactions_train: {len(train)}")
    print(f"  interactions_test: {len(test)}")

    all_interactions = train + test
    type_counts = Counter(str(x.get("interactionType")) for x in all_interactions)
    print("  interaction_type_counts:")
    for key in sorted(type_counts.keys()):
        print(f"    - {key}: {type_counts[key]}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Validate processed OULAD JSON files.")
    parser.add_argument("--processed-dir", type=Path, default=DEFAULT_PROCESSED_DIR)
    parser.add_argument("--strict", action="store_true", help="Treat warnings as failures")
    parser.add_argument(
        "--min-overlap-users",
        type=int,
        default=10,
        help="Recommended minimum number of users present in both train and test.",
    )
    args = parser.parse_args()

    processed_dir = args.processed_dir
    users = load_json_array(processed_dir / "users.json")
    resources = load_json_array(processed_dir / "resources.json")
    train = load_json_array(processed_dir / "interactions_train.json")
    test = load_json_array(processed_dir / "interactions_test.json")

    errors: List[str] = []
    warnings: List[str] = []

    validate_users(users, errors, warnings)
    validate_resources(resources, errors, warnings)
    validate_interactions(
        train=train,
        test=test,
        user_ids={str(x.get("userId")) for x in users},
        resource_ids={str(x.get("id")) for x in resources},
        min_overlap_users=max(1, args.min_overlap_users),
        errors=errors,
        warnings=warnings,
    )
    print_summary(users, resources, train, test)

    if warnings:
        print("\nWarnings:")
        for w in warnings:
            print(f"  - {w}")
    if errors:
        print("\nErrors:")
        for err in errors:
            print(f"  - {err}")

    if errors or (args.strict and warnings):
        raise SystemExit(1)
    print("\nValidation passed.")


if __name__ == "__main__":
    main()
