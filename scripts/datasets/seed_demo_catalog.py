#!/usr/bin/env python3
"""
Seed demo-mode catalog data into PostgreSQL.

Input files (datasets/demo):
  - resources.json
  - users.json
  - user_access_scopes.json
  - interactions.json
  - tasks.json

This script validates schema consistency and generates SQL compatible with the
current EF Core schema, then optionally applies it via psql.
"""

from __future__ import annotations

import argparse
import json
import subprocess
import uuid
from pathlib import Path
from typing import Any
from urllib.parse import urlparse

ROOT_DIR = Path(__file__).resolve().parents[2]
DEFAULT_DEMO_DIR = ROOT_DIR / "datasets" / "demo"
DEFAULT_SQL_PATH = DEFAULT_DEMO_DIR / "seed_demo.sql"

VALID_CONTENT_TYPES = {"Article": 1, "Video": 2, "Quiz": 3}
VALID_ACCESS_TYPES = {
    "NoDirectAccess": 0,
    "ExternalUrl": 1,
    "InternalPlatform": 2,
    "OfflinePhysical": 3,
    "CommunicationChannel": 4,
}
VALID_VISIBILITY = {
    "Global": 1,
    "CourseOnly": 2,
    "GroupOnly": 3,
    "PrivateToUser": 4,
}
VALID_SCOPE_TYPES = {"Course": 1, "Group": 2}
MAX_SCOPE_KEY_LEN = 100
VALID_INTERACTION_TYPES = {"Viewed": 1, "Completed": 2, "Rated": 3, "Skipped": 4}
VALID_TASK_STATUS = {"Pending": 1, "Completed": 2, "Overdue": 3}
MAX_SOURCE_NAME_LEN = 150
MAX_URL_LEN = 2048
MAX_ACCESS_INSTRUCTIONS_LEN = 1000


def load_json_array(path: Path) -> list[dict[str, Any]]:
    with path.open("r", encoding="utf-8") as f:
        data = json.load(f)
    if not isinstance(data, list):
        raise ValueError(f"Expected JSON array in {path}")
    return data


def sql_quote(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def sql_nullable_str(value: str | None) -> str:
    return "NULL" if value is None else sql_quote(value)


def sql_nullable_int(value: int | None) -> str:
    return "NULL" if value is None else str(int(value))


def sql_user_profile_id_subquery(user_id_quoted: str) -> str:
    """Resolves optional FK UserProfileId; NULL if no UserProfiles row for that Identity user id."""
    return f'(SELECT "Id" FROM "UserProfiles" WHERE "UserId" = {user_id_quoted} LIMIT 1)'


def ensure(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def is_safe_http_url(url: str) -> bool:
    parsed = urlparse(url.strip())
    return parsed.scheme in ("http", "https") and bool(parsed.netloc)


def resolve_source_name(row: dict[str, Any]) -> str | None:
    raw = row.get("sourceName")
    if raw is None:
        raw = row.get("source")
    if raw is None:
        return None
    value = str(raw).strip()
    return value or None


def resolve_access_fields(row: dict[str, Any], prefix: str) -> tuple[str | None, str | None, int, str | None, int]:
    source_name = resolve_source_name(row)
    if source_name is not None:
        ensure(len(source_name) <= MAX_SOURCE_NAME_LEN, f"{prefix}: sourceName exceeds {MAX_SOURCE_NAME_LEN} chars")

    url_raw = row.get("url")
    url = None if url_raw is None else str(url_raw).strip() or None
    if url is not None:
        ensure(len(url) <= MAX_URL_LEN, f"{prefix}: url exceeds {MAX_URL_LEN} chars")
        ensure(is_safe_http_url(url), f"{prefix}: url must be absolute http or https")

    access_type_raw = row.get("accessType")
    if access_type_raw is None:
        access_type = VALID_ACCESS_TYPES["ExternalUrl" if url else "NoDirectAccess"]
    else:
        access_type_name = str(access_type_raw)
        ensure(access_type_name in VALID_ACCESS_TYPES, f"{prefix}: invalid accessType '{access_type_name}'")
        access_type = VALID_ACCESS_TYPES[access_type_name]

    instructions_raw = row.get("accessInstructions")
    instructions = None if instructions_raw is None else str(instructions_raw).strip() or None
    if instructions is not None:
        ensure(
            len(instructions) <= MAX_ACCESS_INSTRUCTIONS_LEN,
            f"{prefix}: accessInstructions exceeds {MAX_ACCESS_INSTRUCTIONS_LEN} chars",
        )

    visibility_raw = row.get("visibility")
    if visibility_raw is None:
        visibility = VALID_VISIBILITY["Global"]
    else:
        visibility_name = str(visibility_raw)
        ensure(visibility_name in VALID_VISIBILITY, f"{prefix}: invalid visibility '{visibility_name}'")
        visibility = VALID_VISIBILITY[visibility_name]

    return source_name, url, access_type, instructions, visibility


def validate_resources(resources: list[dict[str, Any]]) -> set[str]:
    ids: set[str] = set()
    for i, row in enumerate(resources, start=1):
        prefix = f"resources[{i}]"
        for key in ("id", "title", "topic", "difficulty", "estimatedDurationMinutes", "contentType"):
            ensure(key in row, f"{prefix}: missing key '{key}'")
        resource_id = str(row["id"])
        ensure(resource_id not in ids, f"{prefix}: duplicate id '{resource_id}'")
        ids.add(resource_id)
        uuid.UUID(resource_id)
        ensure(1 <= int(row["difficulty"]) <= 5, f"{prefix}: difficulty must be 1..5")
        ensure(int(row["estimatedDurationMinutes"]) > 0, f"{prefix}: estimatedDurationMinutes must be > 0")
        ensure(str(row["contentType"]) in VALID_CONTENT_TYPES, f"{prefix}: invalid contentType")
        ensure(len(str(row["title"])) <= 200, f"{prefix}: title exceeds 200 chars (EF MaxLength)")
        ensure(len(str(row["topic"])) <= 100, f"{prefix}: topic exceeds 100 chars (EF MaxLength)")
        validate_resource_access_scopes(row, prefix)
        resolve_access_fields(row, prefix)
    return ids


def validate_resource_access_scopes(row: dict[str, Any], prefix: str) -> None:
    visibility = str(row.get("visibility", "Global"))
    scopes = row.get("accessScopes") or []
    if not isinstance(scopes, list):
        raise ValueError(f"{prefix}: accessScopes must be an array")

    if visibility == "Global":
        return

    if visibility == "PrivateToUser":
        owner = row.get("ownerUserId")
        ensure(owner is not None and str(owner).strip(), f"{prefix}: PrivateToUser requires ownerUserId")
        return

    if visibility in ("CourseOnly", "GroupOnly"):
        expected_type = "Course" if visibility == "CourseOnly" else "Group"
        matching = [s for s in scopes if str(s.get("scopeType")) == expected_type]
        ensure(len(matching) >= 1, f"{prefix}: {visibility} requires at least one {expected_type} accessScope")

    for i, scope in enumerate(scopes, start=1):
        scope_prefix = f"{prefix}.accessScopes[{i}]"
        ensure("scopeType" in scope and "scopeKey" in scope, f"{scope_prefix}: missing scopeType or scopeKey")
        scope_type = str(scope["scopeType"])
        ensure(scope_type in VALID_SCOPE_TYPES, f"{scope_prefix}: invalid scopeType '{scope_type}'")
        scope_key = str(scope["scopeKey"]).strip()
        ensure(scope_key, f"{scope_prefix}: scopeKey is required")
        ensure(len(scope_key) <= MAX_SCOPE_KEY_LEN, f"{scope_prefix}: scopeKey exceeds {MAX_SCOPE_KEY_LEN} chars")


def validate_user_access_scopes(
    memberships: list[dict[str, Any]],
    user_ids: set[str],
) -> None:
    for i, row in enumerate(memberships, start=1):
        prefix = f"user_access_scopes[{i}]"
        for key in ("userId", "scopeType", "scopeKey"):
            ensure(key in row, f"{prefix}: missing key '{key}'")
        ensure(str(row["userId"]) in user_ids, f"{prefix}: unknown userId")
        scope_type = str(row["scopeType"])
        ensure(scope_type in VALID_SCOPE_TYPES, f"{prefix}: invalid scopeType '{scope_type}'")
        scope_key = str(row["scopeKey"]).strip()
        ensure(scope_key, f"{prefix}: scopeKey is required")
        ensure(len(scope_key) <= MAX_SCOPE_KEY_LEN, f"{prefix}: scopeKey exceeds {MAX_SCOPE_KEY_LEN} chars")


def validate_users(users: list[dict[str, Any]]) -> set[str]:
    user_ids: set[str] = set()
    for i, row in enumerate(users, start=1):
        prefix = f"users[{i}]"
        for key in ("userId", "dailyAvailableMinutes", "level", "xp"):
            ensure(key in row, f"{prefix}: missing key '{key}'")
        user_id = str(row["userId"])
        ensure(user_id not in user_ids, f"{prefix}: duplicate userId '{user_id}'")
        user_ids.add(user_id)
        ensure(int(row["dailyAvailableMinutes"]) > 0, f"{prefix}: dailyAvailableMinutes must be > 0")
        ensure(int(row["level"]) >= 1, f"{prefix}: level must be >= 1")
        ensure(int(row["xp"]) >= 0, f"{prefix}: xp must be >= 0")
    return user_ids


def validate_interactions(interactions: list[dict[str, Any]], user_ids: set[str], resource_ids: set[str]) -> None:
    for i, row in enumerate(interactions, start=1):
        prefix = f"interactions[{i}]"
        for key in ("userId", "learningResourceId", "interactionType"):
            ensure(key in row, f"{prefix}: missing key '{key}'")
        ensure(str(row["userId"]) in user_ids, f"{prefix}: unknown userId")
        ensure(str(row["learningResourceId"]) in resource_ids, f"{prefix}: unknown learningResourceId")
        ensure(str(row["interactionType"]) in VALID_INTERACTION_TYPES, f"{prefix}: invalid interactionType")
        rating = row.get("rating")
        if rating is not None:
            ensure(1 <= int(rating) <= 5, f"{prefix}: rating must be 1..5")


def validate_tasks(tasks: list[dict[str, Any]], user_ids: set[str], resource_ids: set[str]) -> None:
    for i, row in enumerate(tasks, start=1):
        prefix = f"tasks[{i}]"
        for key in ("userId", "title", "deadlineUtc", "estimatedMinutes", "priority", "status"):
            ensure(key in row, f"{prefix}: missing key '{key}'")
        ensure(str(row["userId"]) in user_ids, f"{prefix}: unknown userId")
        lrid = row.get("learningResourceId")
        if lrid is not None:
            ensure(str(lrid) in resource_ids, f"{prefix}: unknown learningResourceId")
        ensure(1 <= int(row["priority"]) <= 5, f"{prefix}: priority must be 1..5")
        ensure(int(row["estimatedMinutes"]) > 0, f"{prefix}: estimatedMinutes must be > 0")
        ensure(str(row["status"]) in VALID_TASK_STATUS, f"{prefix}: invalid status")


def append_access_scope_sql(
    lines: list[str],
    resources: list[dict[str, Any]],
    user_access_scopes: list[dict[str, Any]],
) -> None:
    for res in resources:
        resource_id = str(res["id"])
        for scope in res.get("accessScopes") or []:
            scope_type = VALID_SCOPE_TYPES[str(scope["scopeType"])]
            scope_key = str(scope["scopeKey"]).strip()
            scope_id = str(
                uuid.uuid5(
                    uuid.NAMESPACE_URL,
                    f"demo-resource-scope:{resource_id}:{scope['scopeType']}:{scope_key}",
                )
            )
            lines.append(
                "INSERT INTO \"ResourceAccessScopes\" "
                "(\"Id\", \"LearningResourceId\", \"ScopeType\", \"ScopeKey\", \"CreatedAtUtc\") VALUES "
                f"({sql_quote(scope_id)}::uuid, {sql_quote(resource_id)}::uuid, {scope_type}, "
                f"{sql_quote(scope_key)}, NOW()) "
                "ON CONFLICT (\"LearningResourceId\", \"ScopeType\", \"ScopeKey\") DO UPDATE SET "
                "\"UpdatedAtUtc\" = NOW();"
            )

    for row in user_access_scopes:
        user_id = str(row["userId"])
        scope_type = VALID_SCOPE_TYPES[str(row["scopeType"])]
        scope_key = str(row["scopeKey"]).strip()
        membership_id = str(
            uuid.uuid5(
                uuid.NAMESPACE_URL,
                f"demo-user-scope:{user_id}:{row['scopeType']}:{scope_key}",
            )
        )
        lines.append(
            "INSERT INTO \"UserAccessScopeMemberships\" "
            "(\"Id\", \"UserId\", \"ScopeType\", \"ScopeKey\", \"CreatedAtUtc\") VALUES "
            f"({sql_quote(membership_id)}::uuid, {sql_quote(user_id)}, {scope_type}, "
            f"{sql_quote(scope_key)}, NOW()) "
            "ON CONFLICT (\"UserId\", \"ScopeType\", \"ScopeKey\") DO UPDATE SET "
            "\"UpdatedAtUtc\" = NOW();"
        )


def build_sql(
    resources: list[dict[str, Any]],
    users: list[dict[str, Any]],
    user_access_scopes: list[dict[str, Any]],
    interactions: list[dict[str, Any]],
    tasks: list[dict[str, Any]],
    *,
    resources_only: bool = False,
) -> str:
    lines: list[str] = []
    lines.append("-- Auto-generated by scripts/datasets/seed_demo_catalog.py")
    lines.append("BEGIN;")
    lines.append("")
    lines.append("-- Schema guard: expected EF Core PostgreSQL columns (ApplicationDbContext snapshot).")
    lines.append("DO $$")
    lines.append("DECLARE")
    lines.append("    missing_count integer;")
    lines.append("BEGIN")
    lines.append("    WITH required_columns(table_name, column_name) AS (")
    lines.append("        VALUES")
    lines.append("            ('UserProfiles', 'Id'),")
    lines.append("            ('UserProfiles', 'UserId'),")
    lines.append("            ('UserProfiles', 'Level'),")
    lines.append("            ('UserProfiles', 'Xp'),")
    lines.append("            ('UserProfiles', 'DailyAvailableMinutes'),")
    lines.append("            ('UserProfiles', 'CreatedAtUtc'),")
    lines.append("            ('UserProfiles', 'UpdatedAtUtc'),")
    lines.append("            ('UserPreferences', 'Id'),")
    lines.append("            ('UserPreferences', 'UserProfileId'),")
    lines.append("            ('UserPreferences', 'PreferredDifficulty'),")
    lines.append("            ('UserPreferences', 'PreferredContentTypesCsv'),")
    lines.append("            ('UserPreferences', 'PreferredTopicsCsv'),")
    lines.append("            ('UserPreferences', 'CreatedAtUtc'),")
    lines.append("            ('UserPreferences', 'UpdatedAtUtc'),")
    lines.append("            ('LearningResources', 'Id'),")
    lines.append("            ('LearningResources', 'Title'),")
    lines.append("            ('LearningResources', 'Description'),")
    lines.append("            ('LearningResources', 'Topic'),")
    lines.append("            ('LearningResources', 'Difficulty'),")
    lines.append("            ('LearningResources', 'EstimatedDurationMinutes'),")
    lines.append("            ('LearningResources', 'ContentType'),")
    lines.append("            ('LearningResources', 'SourceName'),")
    lines.append("            ('LearningResources', 'Url'),")
    lines.append("            ('LearningResources', 'AccessType'),")
    lines.append("            ('LearningResources', 'AccessInstructions'),")
    lines.append("            ('LearningResources', 'Visibility'),")
    lines.append("            ('LearningResources', 'OwnerUserId'),")
    lines.append("            ('LearningResources', 'CreatedAtUtc'),")
    lines.append("            ('ResourceAccessScopes', 'Id'),")
    lines.append("            ('ResourceAccessScopes', 'LearningResourceId'),")
    lines.append("            ('ResourceAccessScopes', 'ScopeType'),")
    lines.append("            ('ResourceAccessScopes', 'ScopeKey'),")
    lines.append("            ('ResourceAccessScopes', 'CreatedAtUtc'),")
    lines.append("            ('UserAccessScopeMemberships', 'Id'),")
    lines.append("            ('UserAccessScopeMemberships', 'UserId'),")
    lines.append("            ('UserAccessScopeMemberships', 'ScopeType'),")
    lines.append("            ('UserAccessScopeMemberships', 'ScopeKey'),")
    lines.append("            ('UserAccessScopeMemberships', 'CreatedAtUtc'),")
    lines.append("            ('LearningResources', 'UpdatedAtUtc'),")
    lines.append("            ('UserInteractions', 'Id'),")
    lines.append("            ('UserInteractions', 'UserId'),")
    lines.append("            ('UserInteractions', 'LearningResourceId'),")
    lines.append("            ('UserInteractions', 'InteractionType'),")
    lines.append("            ('UserInteractions', 'Rating'),")
    lines.append("            ('UserInteractions', 'TimeSpentMinutes'),")
    lines.append("            ('UserInteractions', 'UserProfileId'),")
    lines.append("            ('UserInteractions', 'CreatedAtUtc'),")
    lines.append("            ('UserInteractions', 'UpdatedAtUtc'),")
    lines.append("            ('StudyTasks', 'Id'),")
    lines.append("            ('StudyTasks', 'UserId'),")
    lines.append("            ('StudyTasks', 'LearningResourceId'),")
    lines.append("            ('StudyTasks', 'Title'),")
    lines.append("            ('StudyTasks', 'Notes'),")
    lines.append("            ('StudyTasks', 'DeadlineUtc'),")
    lines.append("            ('StudyTasks', 'EstimatedMinutes'),")
    lines.append("            ('StudyTasks', 'Priority'),")
    lines.append("            ('StudyTasks', 'Status'),")
    lines.append("            ('StudyTasks', 'UserProfileId'),")
    lines.append("            ('StudyTasks', 'CreatedAtUtc'),")
    lines.append("            ('StudyTasks', 'UpdatedAtUtc')")
    lines.append("    )")
    lines.append("    SELECT COUNT(*) INTO missing_count")
    lines.append("    FROM required_columns rc")
    lines.append("    LEFT JOIN information_schema.columns c")
    lines.append("      ON c.table_schema = current_schema()")
    lines.append("     AND c.table_name = rc.table_name")
    lines.append("     AND c.column_name = rc.column_name")
    lines.append("    WHERE c.column_name IS NULL;")
    lines.append("    IF missing_count > 0 THEN")
    lines.append(
        "        RAISE EXCEPTION 'Schema guard failed: expected EF Core columns not found in schema %', current_schema();"
    )
    lines.append("    END IF;")
    lines.append("END $$;")
    lines.append("")

    if not resources_only:
        for user in users:
            user_id = str(user["userId"])
            user_id_q = sql_quote(user_id)
            profile_id = str(uuid.uuid5(uuid.NAMESPACE_URL, f"demo-user-profile:{user_id}"))
            profile_id_q = f"{sql_quote(profile_id)}::uuid"
            profile_id_by_user_sql = (
                f"(SELECT \"Id\" FROM \"UserProfiles\" WHERE \"UserId\" = {user_id_q} LIMIT 1)"
            )
            lines.append(
                "UPDATE \"UserProfiles\" SET "
                f"\"Level\" = {int(user['level'])}, "
                f"\"Xp\" = {int(user['xp'])}, "
                f"\"DailyAvailableMinutes\" = {int(user['dailyAvailableMinutes'])}, "
                "\"UpdatedAtUtc\" = NOW() "
                f"WHERE \"UserId\" = {user_id_q};"
            )
            # Avoid PK collision when deterministic profile Id exists for a different UserId (e.g. mixed seeds).
            lines.append(
                "INSERT INTO \"UserProfiles\" "
                "(\"Id\", \"UserId\", \"Level\", \"Xp\", \"DailyAvailableMinutes\", \"CreatedAtUtc\") "
                f"SELECT CASE WHEN EXISTS (SELECT 1 FROM \"UserProfiles\" WHERE \"Id\" = {profile_id_q}) "
                "THEN gen_random_uuid() ELSE "
                f"{profile_id_q} END, {user_id_q}, {int(user['level'])}, {int(user['xp'])}, "
                f"{int(user['dailyAvailableMinutes'])}, NOW() "
                f"WHERE NOT EXISTS (SELECT 1 FROM \"UserProfiles\" WHERE \"UserId\" = {user_id_q});"
            )
            prefs = user.get("preferences") or {}
            pref_id = str(uuid.uuid5(uuid.NAMESPACE_URL, f"demo-user-prefs:{user_id}"))
            pref_id_q = f"{sql_quote(pref_id)}::uuid"
            # Avoid PK collision when deterministic pref Id exists for a different profile (mixed OULAD/demo seeds).
            lines.append(
                "INSERT INTO \"UserPreferences\" "
                "(\"Id\", \"UserProfileId\", \"PreferredDifficulty\", \"PreferredContentTypesCsv\", \"PreferredTopicsCsv\", \"CreatedAtUtc\") "
                f"SELECT CASE WHEN EXISTS (SELECT 1 FROM \"UserPreferences\" WHERE \"Id\" = {pref_id_q}) "
                f"THEN gen_random_uuid() ELSE {pref_id_q} END, "
                f"{profile_id_by_user_sql}, "
                f"{sql_nullable_int(prefs.get('preferredDifficulty'))}, "
                f"{sql_nullable_str(prefs.get('preferredContentTypesCsv'))}, "
                f"{sql_nullable_str(prefs.get('preferredTopicsCsv'))}, NOW() "
                f"WHERE {profile_id_by_user_sql} IS NOT NULL "
                "ON CONFLICT (\"UserProfileId\") DO UPDATE SET "
                "\"PreferredDifficulty\" = EXCLUDED.\"PreferredDifficulty\", "
                "\"PreferredContentTypesCsv\" = EXCLUDED.\"PreferredContentTypesCsv\", "
                "\"PreferredTopicsCsv\" = EXCLUDED.\"PreferredTopicsCsv\", "
                "\"UpdatedAtUtc\" = NOW();"
            )

    for res in resources:
        content_type = VALID_CONTENT_TYPES[str(res["contentType"])]
        source_name, url, access_type, access_instructions, visibility = resolve_access_fields(
            res, f"resource:{res['id']}"
        )
        owner_user_id = res.get("ownerUserId")
        owner_sql = "NULL" if owner_user_id is None else sql_quote(str(owner_user_id))
        lines.append(
            "INSERT INTO \"LearningResources\" "
            "(\"Id\", \"Title\", \"Description\", \"Topic\", \"Difficulty\", \"EstimatedDurationMinutes\", "
            "\"ContentType\", \"SourceName\", \"Url\", \"AccessType\", \"AccessInstructions\", \"Visibility\", "
            "\"OwnerUserId\", \"CreatedAtUtc\") VALUES "
            f"({sql_quote(str(res['id']))}::uuid, {sql_quote(str(res['title']))}, {sql_nullable_str(res.get('description'))}, "
            f"{sql_quote(str(res['topic']))}, {int(res['difficulty'])}, {int(res['estimatedDurationMinutes'])}, {content_type}, "
            f"{sql_nullable_str(source_name)}, {sql_nullable_str(url)}, {access_type}, "
            f"{sql_nullable_str(access_instructions)}, {visibility}, {owner_sql}, NOW()) "
            "ON CONFLICT (\"Id\") DO UPDATE SET "
            "\"Title\" = EXCLUDED.\"Title\", "
            "\"Description\" = EXCLUDED.\"Description\", "
            "\"Topic\" = EXCLUDED.\"Topic\", "
            "\"Difficulty\" = EXCLUDED.\"Difficulty\", "
            "\"EstimatedDurationMinutes\" = EXCLUDED.\"EstimatedDurationMinutes\", "
            "\"ContentType\" = EXCLUDED.\"ContentType\", "
            "\"SourceName\" = EXCLUDED.\"SourceName\", "
            "\"Url\" = EXCLUDED.\"Url\", "
            "\"AccessType\" = EXCLUDED.\"AccessType\", "
            "\"AccessInstructions\" = EXCLUDED.\"AccessInstructions\", "
            "\"Visibility\" = EXCLUDED.\"Visibility\", "
            "\"OwnerUserId\" = EXCLUDED.\"OwnerUserId\", "
            "\"UpdatedAtUtc\" = NOW();"
        )

    append_access_scope_sql(lines, resources, user_access_scopes)

    if resources_only:
        lines.append("")
        lines.append("COMMIT;")
        return "\n".join(lines) + "\n"

    for row in interactions:
        rec_key = (
            f"demo-interaction:{row['userId']}:{row['learningResourceId']}:{row['interactionType']}:"
            f"{row.get('createdAtUtc', '')}"
        )
        interaction_id = str(uuid.uuid5(uuid.NAMESPACE_URL, rec_key))
        interaction_type = VALID_INTERACTION_TYPES[str(row["interactionType"])]
        created_at_sql = "NOW()" if not row.get("createdAtUtc") else f"{sql_quote(str(row['createdAtUtc']))}::timestamptz"
        uid_q = sql_quote(str(row["userId"]))
        profile_sql = sql_user_profile_id_subquery(uid_q)
        lines.append(
            "INSERT INTO \"UserInteractions\" "
            "(\"Id\", \"UserId\", \"LearningResourceId\", \"InteractionType\", \"Rating\", \"TimeSpentMinutes\", "
            "\"UserProfileId\", \"CreatedAtUtc\") VALUES "
            f"({sql_quote(interaction_id)}::uuid, {uid_q}, {sql_quote(str(row['learningResourceId']))}::uuid, "
            f"{interaction_type}, {sql_nullable_int(row.get('rating'))}, {sql_nullable_int(row.get('timeSpentMinutes'))}, "
            f"{profile_sql}, {created_at_sql}) "
            "ON CONFLICT (\"Id\") DO NOTHING;"
        )

    for row in tasks:
        task_key = f"demo-task:{row['userId']}:{row['title']}:{row['deadlineUtc']}"
        task_id = str(uuid.uuid5(uuid.NAMESPACE_URL, task_key))
        status = VALID_TASK_STATUS[str(row["status"])]
        learning_resource_id = row.get("learningResourceId")
        learning_resource_sql = "NULL" if learning_resource_id is None else f"{sql_quote(str(learning_resource_id))}::uuid"
        tuid_q = sql_quote(str(row["userId"]))
        task_profile_sql = sql_user_profile_id_subquery(tuid_q)
        lines.append(
            "INSERT INTO \"StudyTasks\" "
            "(\"Id\", \"UserId\", \"LearningResourceId\", \"Title\", \"Notes\", \"DeadlineUtc\", \"EstimatedMinutes\", "
            "\"Priority\", \"Status\", \"UserProfileId\", \"CreatedAtUtc\") VALUES "
            f"({sql_quote(task_id)}::uuid, {tuid_q}, {learning_resource_sql}, {sql_quote(str(row['title']))}, "
            f"{sql_nullable_str(row.get('notes'))}, {sql_quote(str(row['deadlineUtc']))}::timestamptz, "
            f"{int(row['estimatedMinutes'])}, {int(row['priority'])}, {status}, {task_profile_sql}, NOW()) "
            "ON CONFLICT (\"Id\") DO UPDATE SET "
            "\"Notes\" = EXCLUDED.\"Notes\", "
            "\"DeadlineUtc\" = EXCLUDED.\"DeadlineUtc\", "
            "\"EstimatedMinutes\" = EXCLUDED.\"EstimatedMinutes\", "
            "\"Priority\" = EXCLUDED.\"Priority\", "
            "\"Status\" = EXCLUDED.\"Status\", "
            "\"UserProfileId\" = EXCLUDED.\"UserProfileId\", "
            "\"UpdatedAtUtc\" = NOW();"
        )

    lines.append("")
    lines.append("COMMIT;")
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description="Seed demo catalog data.")
    parser.add_argument("--demo-dir", type=Path, default=DEFAULT_DEMO_DIR)
    parser.add_argument("--sql-path", type=Path, default=DEFAULT_SQL_PATH)
    parser.add_argument("--db-url", type=str, default="")
    parser.add_argument("--apply-sql", action="store_true")
    parser.add_argument(
        "--resources-only",
        action="store_true",
        help="Upsert LearningResources only (skips users/interactions/tasks). Use after schema changes when profiles already exist.",
    )
    args = parser.parse_args()

    resources = load_json_array(args.demo_dir / "resources.json")
    users = load_json_array(args.demo_dir / "users.json")
    access_scopes_path = args.demo_dir / "user_access_scopes.json"
    user_access_scopes = load_json_array(access_scopes_path) if access_scopes_path.exists() else []
    interactions = load_json_array(args.demo_dir / "interactions.json")
    tasks = load_json_array(args.demo_dir / "tasks.json")

    resource_ids = validate_resources(resources)
    user_ids = validate_users(users)
    validate_user_access_scopes(user_access_scopes, user_ids)
    validate_interactions(interactions, user_ids, resource_ids)
    validate_tasks(tasks, user_ids, resource_ids)

    sql = build_sql(
        resources,
        users,
        user_access_scopes,
        interactions,
        tasks,
        resources_only=args.resources_only,
    )
    args.sql_path.parent.mkdir(parents=True, exist_ok=True)
    args.sql_path.write_text(sql, encoding="utf-8")
    print(f"Generated SQL: {args.sql_path}")
    mode = "resources-only" if args.resources_only else "full"
    print(
        f"Validated rows ({mode}): resources={len(resources)} users={len(users)} "
        f"interactions={len(interactions)} tasks={len(tasks)}"
    )

    if args.apply_sql:
        if not args.db_url:
            raise ValueError("--db-url is required when using --apply-sql")
        cmd = ["psql", args.db_url, "-v", "ON_ERROR_STOP=1", "-f", str(args.sql_path)]
        subprocess.run(cmd, check=True)
        print("Demo SQL import completed (COMMIT).")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
