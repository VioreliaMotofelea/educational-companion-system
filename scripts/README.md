# Scripts

Run from the **repository root** with `.venv` activated (`pip install -r ai-service/requirements.txt`).

## `scripts/datasets/` — OULAD pipeline

| Script | Purpose |
|--------|---------|
| `preprocess_oulad.py` | Raw CSV → `datasets/oulad/processed/*.json` |
| `validate_processed.py` | Consistency checks |
| `seed_oulad.py` | Generate/apply SQL to PostgreSQL |
| `seed_demo_catalog.py` | Validate demo JSON → SQL |
| `run_oulad_content_mode_experiments.py` | Offline 7-strategy evaluation |
| `generate_eval_n5000_thesis_bundle.py` | Rebuild `datasets/oulad/processed/eval_n5000/` |
| `enrich_oulad_resources_for_eval.py` | Enrich resource text for content modes |

Typical sequence:

```bash
source .venv/bin/activate
python scripts/datasets/preprocess_oulad.py --modules AAA BBB --max-users 5000
python scripts/datasets/validate_processed.py
python scripts/datasets/seed_oulad.py --mode sql --apply-sql --db-url "postgresql://USER:PASS@localhost:5432/educational_companion_db"
# start backend + ai-service, then:
python scripts/datasets/run_oulad_content_mode_experiments.py
```

## `scripts/demo/` — ingestion helpers

| Script | Purpose |
|--------|---------|
| `generate_binary_demo_files.py` | Regenerate `.docx` / `.pdf` in `datasets/demo/resource-files/` |
| `validate_resource_demo_files.py` | Check expected demo files exist |
| `compare_ingestion_semantic_text.py` | Compare extracted text for semantic experiments |

## Tests

```bash
python -m pytest scripts/datasets
```
