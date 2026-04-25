#!/usr/bin/env python3
"""
Seed processed OULAD JSON data into backend:
  - mode=api: POST resources + interactions to backend API
  - mode=sql: generate SQL (and optionally execute via psql) for direct PostgreSQL import
"""

from __future__ import annotations

import argparse
import json
import subprocess
import uuid
from pathlib import Path
from typing import Dict, Iterable, List

import requests


ROOT_DIR = Path(__file__).resolve().parents[2]
DEFAULT_PROCESSED_DIR = ROOT_DIR / "datasets" / "oulad" / "processed"
DEFAULT_SQL_PATH = DEFAULT_PROCESSED_DIR / "seed_oulad.sql"

CONTENT_TYPE_TO_INT = {"Article": 1, "Video": 2, "Quiz": 3}
INTERACTION_TYPE_TO_INT = {"Viewed": 1, "Completed": 2, "Rated": 3, "Skipped": 4}


def load_json_array(path: Path) -> List[dict]:
    with path.open("r", encoding="utf-8") as f:
        data = json.load(f)
    if not isinstance(data, list):
        raise ValueError(f"Expected JSON array in {path}")
    return data


def chunked(items: List[dict], size: int) -> Iterable[List[dict]]:
    for i in range(0, len(items), size):
        yield items[i : i + size]


def sql_quote(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def sql_nullable_str(value: str | None) -> str:
    if value is None:
        return "NULL"
    return sql_quote(value)


def sql_nullable_int(value: int | None) -> str:
    if value is None:
        return "NULL"
    return str(int(value))


def seed_via_api(
    backend_url: str,
    users: List[dict],
    resources: List[dict],
    interactions: List[dict],
    batch_size: int,
    timeout_s: int,
) -> None:
    base = backend_url.rstrip("/")
    session = requests.Session()
    session.headers.update({"Content-Type": "application/json"})

    print(f"Seeding resources via API: {len(resources)}")
    resource_id_map: Dict[str, str] = {}
    for idx, resource in enumerate(resources, start=1):
        payload = {
            "title": resource["title"],
            "description": resource.get("description"),
            "topic": resource["topic"],
            "difficulty": resource["difficulty"],
            "estimatedDurationMinutes": resource["estimatedDurationMinutes"],
            "contentType": resource["contentType"],
        }
        resp = session.post(f"{base}/api/resources", json=payload, timeout=timeout_s)
        if resp.status_code not in (200, 201):
            raise RuntimeError(f"Resource create failed ({resp.status_code}): {resp.text[:500]}")
        created = resp.json()
        source_id = str(resource["id"])
        target_id = str(created["id"])
        resource_id_map[source_id] = target_id

        if idx % 500 == 0:
            print(f"  created resources: {idx}/{len(resources)}")

    print(f"Checking user profiles existence: {len(users)}")
    existing_users = set()
    for idx, user in enumerate(users, start=1):
        user_id = str(user["userId"])
        resp = session.get(f"{base}/api/users/{user_id}", timeout=timeout_s)
        if resp.status_code == 200:
            existing_users.add(user_id)
        elif resp.status_code != 404:
            raise RuntimeError(f"User lookup failed ({resp.status_code}) for '{user_id}': {resp.text[:500]}")
        if idx % 2000 == 0:
            print(f"  checked users: {idx}/{len(users)}")

    skipped_missing_user = 0
    interactions_payload: List[dict] = []
    for row in interactions:
        user_id = str(row["userId"])
        if user_id not in existing_users:
            skipped_missing_user += 1
            continue
        source_res_id = str(row["learningResourceId"])
        target_res_id = resource_id_map.get(source_res_id)
        if not target_res_id:
            continue
        payload = {
            "userId": user_id,
            "learningResourceId": target_res_id,
            "interactionType": row["interactionType"],
            "rating": row.get("rating"),
            "timeSpentMinutes": row.get("timeSpentMinutes"),
        }
        interactions_payload.append(payload)

    print(f"Seeding interactions via API: {len(interactions_payload)} (skipped missing users: {skipped_missing_user})")
    created = 0
    for batch in chunked(interactions_payload, batch_size):
        for payload in batch:
            resp = session.post(f"{base}/api/interactions", json=payload, timeout=timeout_s)
            if resp.status_code not in (200, 201):
                raise RuntimeError(f"Interaction create failed ({resp.status_code}): {resp.text[:500]}")
            created += 1
        print(f"  created interactions: {created}/{len(interactions_payload)}")

    print("API seed completed.")
    print("Important: API mode cannot create missing users (no POST /api/users endpoint in current backend).")
    if skipped_missing_user:
        print(f"Missing users prevented {skipped_missing_user} interactions from being inserted.")


def build_sql(users: List[dict], resources: List[dict], interactions: List[dict]) -> str:
    lines: List[str] = []
    lines.append("-- Auto-generated by scripts/datasets/seed_oulad.py")
    lines.append("BEGIN;")
    lines.append("")
    lines.append("-- Schema guard: verify expected EF Core tables/columns exist in PostgreSQL.")
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
    lines.append("            ('LearningResources', 'Id'),")
    lines.append("            ('LearningResources', 'Title'),")
    lines.append("            ('LearningResources', 'Description'),")
    lines.append("            ('LearningResources', 'Topic'),")
    lines.append("            ('LearningResources', 'Difficulty'),")
    lines.append("            ('LearningResources', 'EstimatedDurationMinutes'),")
    lines.append("            ('LearningResources', 'ContentType'),")
    lines.append("            ('LearningResources', 'CreatedAtUtc'),")
    lines.append("            ('LearningResources', 'UpdatedAtUtc'),")
    lines.append("            ('UserInteractions', 'Id'),")
    lines.append("            ('UserInteractions', 'UserId'),")
    lines.append("            ('UserInteractions', 'LearningResourceId'),")
    lines.append("            ('UserInteractions', 'InteractionType'),")
    lines.append("            ('UserInteractions', 'Rating'),")
    lines.append("            ('UserInteractions', 'TimeSpentMinutes'),")
    lines.append("            ('UserInteractions', 'CreatedAtUtc'),")
    lines.append("            ('UserInteractions', 'UpdatedAtUtc')")
    lines.append("    )")
    lines.append("    SELECT COUNT(*) INTO missing_count")
    lines.append("    FROM required_columns rc")
    lines.append("    LEFT JOIN information_schema.columns c")
    lines.append("      ON c.table_schema = current_schema()")
    lines.append("     AND c.table_name = rc.table_name")
    lines.append("     AND c.column_name = rc.column_name")
    lines.append("    WHERE c.column_name IS NULL;")
    lines.append("")
    lines.append("    IF missing_count > 0 THEN")
    lines.append("        RAISE EXCEPTION 'Schema guard failed: expected EF Core columns not found in schema %', current_schema();")
    lines.append("    END IF;")
    lines.append("END $$;")
    lines.append("")

    for user in users:
        user_id = str(user["userId"])
        profile_id = str(uuid.uuid5(uuid.NAMESPACE_URL, f"user-profile:{user_id}"))
        lines.append(
            "UPDATE \"UserProfiles\" SET "
            f"\"Level\" = {int(user['level'])}, "
            f"\"Xp\" = {int(user['xp'])}, "
            f"\"DailyAvailableMinutes\" = {int(user['dailyAvailableMinutes'])}, "
            "\"UpdatedAtUtc\" = NOW() "
            f"WHERE \"UserId\" = {sql_quote(user_id)};"
        )
        lines.append(
            "INSERT INTO \"UserProfiles\" "
            "(\"Id\", \"UserId\", \"Level\", \"Xp\", \"DailyAvailableMinutes\", \"CreatedAtUtc\") "
            f"SELECT {sql_quote(profile_id)}::uuid, {sql_quote(user_id)}, {int(user['level'])}, {int(user['xp'])}, "
            f"{int(user['dailyAvailableMinutes'])}, NOW() "
            f"WHERE NOT EXISTS (SELECT 1 FROM \"UserProfiles\" WHERE \"UserId\" = {sql_quote(user_id)});"
        )

    for res in resources:
        content_type = CONTENT_TYPE_TO_INT[str(res["contentType"])]
        lines.append(
            "INSERT INTO \"LearningResources\" "
            "(\"Id\", \"Title\", \"Description\", \"Topic\", \"Difficulty\", \"EstimatedDurationMinutes\", "
            "\"ContentType\", \"CreatedAtUtc\") VALUES "
            f"({sql_quote(str(res['id']))}::uuid, {sql_quote(str(res['title']))}, "
            f"{sql_nullable_str(res.get('description'))}, {sql_quote(str(res['topic']))}, "
            f"{int(res['difficulty'])}, {int(res['estimatedDurationMinutes'])}, {content_type}, NOW()) "
            "ON CONFLICT (\"Id\") DO UPDATE SET "
            "\"Title\" = EXCLUDED.\"Title\", "
            "\"Description\" = EXCLUDED.\"Description\", "
            "\"Topic\" = EXCLUDED.\"Topic\", "
            "\"Difficulty\" = EXCLUDED.\"Difficulty\", "
            "\"EstimatedDurationMinutes\" = EXCLUDED.\"EstimatedDurationMinutes\", "
            "\"ContentType\" = EXCLUDED.\"ContentType\", "
            "\"UpdatedAtUtc\" = NOW();"
        )

    for interaction in interactions:
        rec_key = (
            f"interaction:{interaction['userId']}:{interaction['learningResourceId']}:"
            f"{interaction['interactionType']}:{interaction.get('createdAtUtc', '')}"
        )
        interaction_id = str(uuid.uuid5(uuid.NAMESPACE_URL, rec_key))
        interaction_type = INTERACTION_TYPE_TO_INT[str(interaction["interactionType"])]
        created_at = interaction.get("createdAtUtc")
        created_at_sql = "NOW()" if not created_at else f"{sql_quote(str(created_at))}::timestamptz"

        lines.append(
            "INSERT INTO \"UserInteractions\" "
            "(\"Id\", \"UserId\", \"LearningResourceId\", \"InteractionType\", \"Rating\", \"TimeSpentMinutes\", \"CreatedAtUtc\") VALUES "
            f"({sql_quote(interaction_id)}::uuid, {sql_quote(str(interaction['userId']))}, "
            f"{sql_quote(str(interaction['learningResourceId']))}::uuid, {interaction_type}, "
            f"{sql_nullable_int(interaction.get('rating'))}, {sql_nullable_int(interaction.get('timeSpentMinutes'))}, {created_at_sql}) "
            "ON CONFLICT (\"Id\") DO UPDATE SET "
            "\"InteractionType\" = EXCLUDED.\"InteractionType\", "
            "\"Rating\" = EXCLUDED.\"Rating\", "
            "\"TimeSpentMinutes\" = EXCLUDED.\"TimeSpentMinutes\", "
            "\"UpdatedAtUtc\" = NOW();"
        )

    lines.append("COMMIT;")
    lines.append("")
    return "\n".join(lines)


def apply_sql(sql_path: Path, db_url: str) -> None:
    cmd = ["psql", db_url, "-v", "ON_ERROR_STOP=1", "-f", str(sql_path)]
    print(f"Executing SQL using psql: {' '.join(cmd[:3])} ...")
    subprocess.run(cmd, check=True)


def verify_db_schema(db_url: str, schema: str = "public") -> None:
    required_columns = [
        ("UserProfiles", "Id"),
        ("UserProfiles", "UserId"),
        ("UserProfiles", "Level"),
        ("UserProfiles", "Xp"),
        ("UserProfiles", "DailyAvailableMinutes"),
        ("UserProfiles", "CreatedAtUtc"),
        ("UserProfiles", "UpdatedAtUtc"),
        ("LearningResources", "Id"),
        ("LearningResources", "Title"),
        ("LearningResources", "Description"),
        ("LearningResources", "Topic"),
        ("LearningResources", "Difficulty"),
        ("LearningResources", "EstimatedDurationMinutes"),
        ("LearningResources", "ContentType"),
        ("LearningResources", "CreatedAtUtc"),
        ("LearningResources", "UpdatedAtUtc"),
        ("UserInteractions", "Id"),
        ("UserInteractions", "UserId"),
        ("UserInteractions", "LearningResourceId"),
        ("UserInteractions", "InteractionType"),
        ("UserInteractions", "Rating"),
        ("UserInteractions", "TimeSpentMinutes"),
        ("UserInteractions", "CreatedAtUtc"),
        ("UserInteractions", "UpdatedAtUtc"),
    ]
    values_sql = ", ".join(f"('{t}','{c}')" for t, c in required_columns)
    schema_q = schema.replace("'", "''")

    query = f"""
WITH required_columns(table_name, column_name) AS (
  VALUES {values_sql}
)
SELECT rc.table_name, rc.column_name
FROM required_columns rc
LEFT JOIN information_schema.columns c
  ON c.table_schema = '{schema_q}'
 AND c.table_name = rc.table_name
 AND c.column_name = rc.column_name
WHERE c.column_name IS NULL
ORDER BY rc.table_name, rc.column_name;
"""
    cmd = ["psql", db_url, "-At", "-F", "|", "-c", query]
    result = subprocess.run(cmd, check=True, capture_output=True, text=True)
    missing_lines = [line.strip() for line in result.stdout.splitlines() if line.strip()]
    if missing_lines:
        print("Schema verification failed. Missing columns:")
        for line in missing_lines:
            table_name, column_name = line.split("|", 1)
            print(f"  - {table_name}.{column_name}")
        raise SystemExit(1)

    unique_query = f"""
SELECT COUNT(*)
FROM pg_constraint c
JOIN pg_class t ON t.oid = c.conrelid
JOIN pg_namespace n ON n.oid = t.relnamespace
JOIN unnest(c.conkey) AS cols(attnum) ON true
JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = cols.attnum
WHERE n.nspname = '{schema_q}'
  AND t.relname = 'UserProfiles'
  AND c.contype IN ('u', 'p')
GROUP BY c.oid
HAVING BOOL_AND(a.attname = 'UserId');
"""
    unique_res = subprocess.run(
        ["psql", db_url, "-At", "-c", unique_query], check=True, capture_output=True, text=True
    )
    has_unique_userid = bool(unique_res.stdout.strip())
    print("Schema verification passed.")
    if has_unique_userid:
        print("Found unique/primary constraint on UserProfiles.UserId.")
    else:
        print("No unique constraint on UserProfiles.UserId (expected by current migration).")


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed processed OULAD JSON into backend API or PostgreSQL.")
    parser.add_argument("--processed-dir", type=Path, default=DEFAULT_PROCESSED_DIR)
    parser.add_argument("--mode", choices=["api", "sql"], default="api")
    parser.add_argument("--backend-url", default="http://localhost:5235")
    parser.add_argument("--batch-size", type=int, default=500)
    parser.add_argument("--timeout-s", type=int, default=30)
    parser.add_argument("--include-test", action="store_true", help="Include interactions_test.json in seed.")
    parser.add_argument("--sql-output", type=Path, default=DEFAULT_SQL_PATH)
    parser.add_argument("--apply-sql", action="store_true", help="Run generated SQL via psql.")
    parser.add_argument("--db-url", default="", help="PostgreSQL URL for psql when --apply-sql is used.")
    parser.add_argument("--verify-db-schema", action="store_true", help="Verify live PostgreSQL schema via psql.")
    parser.add_argument("--db-schema", default="public", help="Schema name used by EF tables (default: public).")
    args = parser.parse_args()

    users = load_json_array(args.processed_dir / "users.json")
    resources = load_json_array(args.processed_dir / "resources.json")
    train = load_json_array(args.processed_dir / "interactions_train.json")
    test = load_json_array(args.processed_dir / "interactions_test.json")
    interactions = train + test if args.include_test else train

    if args.verify_db_schema:
        if not args.db_url.strip():
            raise SystemExit("--db-url is required when --verify-db-schema is enabled.")
        verify_db_schema(args.db_url.strip(), schema=args.db_schema.strip() or "public")

    if args.mode == "api":
        seed_via_api(
            backend_url=args.backend_url,
            users=users,
            resources=resources,
            interactions=interactions,
            batch_size=max(1, args.batch_size),
            timeout_s=max(5, args.timeout_s),
        )
        return

    sql = build_sql(users=users, resources=resources, interactions=interactions)
    args.sql_output.parent.mkdir(parents=True, exist_ok=True)
    args.sql_output.write_text(sql, encoding="utf-8")
    print(f"SQL generated: {args.sql_output}")

    if args.apply_sql:
        if not args.db_url.strip():
            raise SystemExit("--db-url is required when --apply-sql is enabled.")
        apply_sql(args.sql_output, args.db_url.strip())
        print("SQL import completed.")


if __name__ == "__main__":
    main()
