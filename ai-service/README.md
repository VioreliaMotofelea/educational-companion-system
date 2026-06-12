# AI Service — Hybrid Recommendation Engine

Python **FastAPI** microservice that generates personalized learning resource recommendations. It reads the per-user accessible catalogue, interactions, and mastery from the backend; scores candidates with a hybrid model; writes ranked batches via `POST /api/users/{id}/recommendations`.

## Hybrid model (default)

```
FinalScore = 0.5 × Content + 0.3 × Collaborative + 0.2 × DifficultyMatch
```

- **ContentScore** — TF–IDF cosine similarity over title, topic, description (and extracted text summary when present on accessible resources)
- **CollaborativeScore** — user–user cosine similarity on completed-resource matrix
- **DifficultyMatch** — alignment between resource difficulty and EDM suggested level (1–5)

`variant=no_difficulty` renormalizes content/collaborative weights only (OULAD ablation).

## Package layout

| Directory | Purpose |
|-----------|---------|
| `api/` | FastAPI routes, exception handlers |
| `clients/` | HTTP client for backend endpoints |
| `models/` | Pydantic DTOs |
| `recommender/` | Content-based (TF–IDF, optional semantic), collaborative (KNN cosine), hybrid fusion, difficulty match |
| `evaluation/` | Offline logging and graph helpers |
| `tests/` | pytest suite |
| `config.py` | Weights, backend URL, feature flags |

## Configuration (`config.py` / env)

| Variable | Default | Meaning |
|----------|---------|---------|
| `BACKEND_BASE_URL` | `http://localhost:5235` | Backend API root |
| `CONTENT_FUSION_MODE` | `tfidf_only` | `tfidf_only`, `semantic_only`, `tfidf_semantic` |
| `SEMANTIC_CONTENT_ENABLED` | `false` | Sentence-transformer embeddings |

## Install & run

```bash
pip install -r requirements.txt
cd ai-service
source ../.venv/bin/activate   # if not already active
uvicorn main:app --reload --port 8001
```

Or with semantic mode enabled:

```bash
export SEMANTIC_CONTENT_ENABLED=true
export CONTENT_FUSION_MODE=semantic_only
export SEMANTIC_MODEL_NAME=sentence-transformers/all-MiniLM-L6-v2
uvicorn main:app --reload --port 8001
```

http://localhost:8001 · Swagger `/docs`

**Endpoint:** `POST /generate/{user_id}`

```bash
curl -X POST "http://localhost:8001/generate/demo-alex" -H "Content-Type: application/json" -d "{}"
```

Backend must be running. The backend can also trigger generation via `/api/users/{id}/recommendations/generate`.

## Test

```bash
python -m pytest ai-service/tests
```

Offline OULAD batch runs: `python scripts/datasets/run_oulad_content_mode_experiments.py` from the repo root.
