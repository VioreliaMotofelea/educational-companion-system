# Datasets

Static data for the application and offline evaluation. Runtime state is in PostgreSQL.

## `demo/`

Small curated catalog for local development and ingestion.

| File | Content |
|------|---------|
| `resources.json` | Learning resources (topic, difficulty, access metadata) |
| `users.json` | Users (`demo-alex`, `demo-bianca`, …) |
| `interactions.json` | Sample interaction history |
| `tasks.json` | Sample study tasks |
| `resource-files/` | **Educational sample documents** for ingestion (`.md`, `.txt`, `.docx`, `.pdf`) — these are real upload targets, not documentation |

Seed via Development `DatabaseSeeder` or `python scripts/datasets/seed_demo_catalog.py`.

## `oulad/`

Open University Learning Analytics Dataset — research only.

| Path | Content |
|------|---------|
| `raw/` | Place downloaded OULAD CSV files here (local only) |
| `processed/` | Preprocessed JSON, SQL seed, evaluation bundles |
| `processed/eval_n5000/` | Evaluation summary (7 strategies, 4081 users) |
