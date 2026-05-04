# OULAD Integration Tracker

This document tracks what is implemented for OULAD integration and what each related file does.

## Current status (implemented)

- [x] OULAD raw -> processed JSON pipeline
- [x] Processed dataset validation script
- [x] Seed/import script (API mode + SQL mode)
- [x] One-command orchestration script
- [x] `.gitignore` updated for dataset folders and zip files
- [x] Train/test split changed to per-user chronological split
- [x] Interaction mapping includes both `Viewed` and `Completed`
- [x] Subset controls added (`--modules`, `--max-users`)

---

## File-by-file responsibilities

## `scripts/datasets/preprocess_oulad.py`

Purpose:
- Reads OULAD raw CSV files.
- Builds normalized output JSON files for backend + recommender.

Main behavior:
- Builds users from `studentInfo.csv`.
- Aggregates interactions from large `studentVle.csv` in chunks.
- Builds resources from `vle.csv`.
- Uses `assessments.csv` + `studentAssessment.csv` for optional rating mapping.
- Maps click intensity to interaction types:
  - below threshold -> `Viewed`
  - at/above threshold -> `Completed`
- Uses per-user chronological train/test split.

Output files:
- `datasets/oulad/processed/users.json`
- `datasets/oulad/processed/resources.json`
- `datasets/oulad/processed/interactions_train.json`
- `datasets/oulad/processed/interactions_test.json`

Important CLI options:
- `--modules AAA BBB`
- `--max-users 3000`
- `--chunksize`
- `--min-clicks-for-completed`
- `--test-user-percent`

---

## `scripts/datasets/validate_processed.py`

Purpose:
- Validates processed JSON files before import/seed.

Checks:
- Required keys and basic types.
- Allowed enum values (`contentType`, `interactionType`).
- Referential integrity (`userId`, `learningResourceId`).
- Duplicate overlap between train and test.
- Basic chronological split consistency (warning-level checks).

Exit behavior:
- Fails on errors.
- Optional strict mode fails on warnings too (`--strict`).

---

## `scripts/datasets/seed_oulad.py`

Purpose:
- Imports processed data into backend ecosystem.

Mode `api`:
- Creates resources through `POST /api/resources`.
- Checks existing users with `GET /api/users/{id}`.
- Creates interactions through `POST /api/interactions`.
- Note: cannot create users (no `POST /api/users` endpoint currently).

Mode `sql`:
- Generates deterministic SQL upserts for:
  - `UserProfiles`
  - `LearningResources`
  - `UserInteractions`
- Optional direct execution with `psql` (`--apply-sql --db-url ...`).

---

## `scripts/datasets/run_oulad_pipeline.py`

Purpose:
- One-command orchestrator for the full workflow.

Execution order:
1. Run `preprocess_oulad.py`
2. Run `validate_processed.py`
3. Optionally run `seed_oulad.py` (`--seed-mode api|sql|none`)

Use this as the default entrypoint for repeatable experiments.

---

## `scripts/datasets/RUN_COMMANDS.md`

Purpose:
- Operational cheat sheet with all commands for:
  - preprocess
  - validate
  - seed via API
  - seed via SQL
  - one-command end-to-end pipeline

---

## `datasets/oulad/raw/*`

Purpose:
- Source OULAD CSV files (UCI ZIP extracted content).

Status:
- Input-only data for preprocessing.

---

## `datasets/oulad/processed/*`

Purpose:
- Generated artifacts consumed by seed/import.

Core files:
- `users.json`
- `resources.json`
- `interactions_train.json`
- `interactions_test.json`
- optionally `seed_oulad.sql` (generated in SQL mode)

---

## Known constraints / decisions

- Backend expects resource IDs as UUID-like values.
- Current backend API does not provide user creation endpoint.
- For large-scale import, SQL mode is preferred over API mode.
- Recommender relies strongly on `Completed` interactions; `Viewed` is kept for realism.

---

## Next suggested milestones

- [ ] Add backend endpoint for bulk user create (optional but useful)
- [ ] Add a short data-quality report export (`.json` summary)
- [ ] Add a reproducible experiment profile (small/medium/full presets)
- [ ] Add MOOCCubeX adapter as separate preprocessing script
