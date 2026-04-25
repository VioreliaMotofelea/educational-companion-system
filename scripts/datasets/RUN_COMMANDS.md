# OULAD Pipeline - Run Commands

This file contains practical commands for running the OULAD data pipeline end-to-end.

## 0) Prerequisites

Run from repository root:

```bash
cd /Users/viorelia/Desktop/ubb/licenta/educational-companion-system
```

Install Python dependencies (at least once):

```bash
pip install -r ai-service/requirements.txt
```

---

## 1) Preprocess only

Generate:
- `datasets/oulad/processed/users.json`
- `datasets/oulad/processed/resources.json`
- `datasets/oulad/processed/interactions_train.json`
- `datasets/oulad/processed/interactions_test.json`

```bash
python3 scripts/datasets/preprocess_oulad.py
```

### Preprocess on subset (faster)

```bash
python3 scripts/datasets/preprocess_oulad.py --modules AAA BBB --max-users 3000 --chunksize 200000
```

---

## 2) Validate processed JSON

```bash
python3 scripts/datasets/validate_processed.py
```

Strict mode (fails on warnings too):

```bash
python3 scripts/datasets/validate_processed.py --strict
```

---

## 3) Seed/import data

## 3A) Seed via backend API (good for demo, slower for very large imports)

Important: current backend does not expose `POST /api/users`, so users must already exist for interactions to be inserted.

```bash
python3 scripts/datasets/seed_oulad.py --mode api --backend-url http://localhost:5235
```

Include test interactions as well:

```bash
python3 scripts/datasets/seed_oulad.py --mode api --backend-url http://localhost:5235 --include-test
```

## 3B) Seed via SQL (recommended for large experiments)

Generate SQL only:

```bash
python3 scripts/datasets/seed_oulad.py --mode sql --sql-output datasets/oulad/processed/seed_oulad.sql
```

Generate SQL and apply directly with `psql`:

```bash
python3 scripts/datasets/seed_oulad.py --mode sql --apply-sql --db-url "postgresql://USER:PASSWORD@localhost:5432/DB_NAME"
```

---

## 4) One-command pipeline (recommended)

Run preprocess + validate only:

```bash
python3 scripts/datasets/run_oulad_pipeline.py --seed-mode none
```

Run full pipeline with API seeding:

```bash
python3 scripts/datasets/run_oulad_pipeline.py --seed-mode api --backend-url http://localhost:5235
```

Run full pipeline with SQL generation:

```bash
python3 scripts/datasets/run_oulad_pipeline.py --seed-mode sql --sql-output datasets/oulad/processed/seed_oulad.sql
```

Run full pipeline with SQL generation + apply:

```bash
python3 scripts/datasets/run_oulad_pipeline.py --seed-mode sql --apply-sql --db-url "postgresql://USER:PASSWORD@localhost:5432/DB_NAME"
```

Fast end-to-end smoke test:

```bash
python3 scripts/datasets/run_oulad_pipeline.py --modules AAA BBB --max-users 3000 --seed-mode none
```

---

## 5) Suggested execution order for your thesis workflow

1. `--seed-mode none` on a subset (`--modules`, `--max-users`) until validation is clean.
2. Full preprocess + validate on all OULAD.
3. Seed via SQL for large-scale experiment.
4. Start backend + ai-service and run recommendation flow.
