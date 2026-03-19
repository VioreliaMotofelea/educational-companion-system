# AI Service — Evaluation Module

This document describes the evaluation module architecture, metric definitions, data flow, and a reproducible test protocol for the AI recommender service.

---

## 1) Scope and Goal

The evaluation module measures recommendation quality in two complementary ways:
- **Ranking quality** (how well top-k matches user relevance)
- **Behavioral/business quality** (clicks and completions)
- **Diversity and discovery quality** (catalog spread, intra-list variety, novelty)

Current metrics:
- `precision@k`, `recall@k`, `ndcg@k`
- `ctr`, `completion_rate`
- `coverage`, `diversity`, `novelty`

---

## 2) Data Source and Pipeline

### Runtime source

These logs are **not mock by default**. They are generated from real app/API usage:
1. `POST /generate/{user_id}` creates recommendations and appends a recommendation session.
2. `POST /evaluation/click` records click events.
3. `POST /evaluation/completion` records completion events.
4. `GET /evaluation/report` computes aggregated metrics.

### Persistence

- File: `evaluation/recommendation_logs.json`
- Config: `config.py` -> `EVALUATION_LOG_FILE`
- Tracking logic: `evaluation/tracking.py`

Session schema:

```json
{
  "user_id": "user-2",
  "recommended_items": ["id1", "id2", "id3", "id4", "id5"],
  "clicked_items": ["id3"],
  "completed_items": ["id3"]
}
```

---

## 3) Metrics and Definitions

Implementation files:
- `evaluation/metrics.py`
- `evaluation/evaluator.py`

Relevance mapping for ranking metrics:
- clicked item -> relevance `1`
- completed item -> relevance `2`

### 3.1 Precision@k

`precision@k = (# relevant items in top-k) / k`

In current implementation, for precision/recall, relevance is binary:
- relevant = clicked union completed.

### 3.2 Recall@k

`recall@k = (# relevant items in top-k) / (# relevant items total)`

### 3.3 nDCG@k

Uses graded relevance and rank discount:
- `DCG@k = sum((2^rel_i - 1) / log2(i + 2))`
- `nDCG@k = DCG@k / IDCG@k`
- `IDCG@k` is computed on ideal ranking (relevance sorted descending).

### 3.4 CTR

`ctr = total clicked / total recommended`

### 3.5 Completion rate

`completion_rate = total completed / total recommended`

### 3.6 Coverage (catalog-level diversity)

`coverage = # unique recommended items / # catalog items`

- catalog size is fetched from backend (`GET /api/resources`)
- unique recommendations are aggregated across logged sessions

### 3.7 Diversity (intra-list)

Average pairwise dissimilarity inside top-k list.

Current lightweight dissimilarity uses:
- `topic` mismatch
- `contentType` mismatch
- normalized `difficulty` gap

Final diversity is normalized to `[0, 1]`.

### 3.8 Novelty

Average self-information of recommended items based on popularity:
- popularity estimated from all interactions (`GET /api/interactions`)
- less popular items -> higher novelty
- score normalized to `[0, 1]`

---

## 4) API Integration

Main route file: `api/routes.py`

- `POST /generate/{user_id}`
  - generates hybrid recommendations
  - pushes to backend
  - appends evaluation session
- `POST /evaluation/click`
  - request: `{ "user_id": "...", "item_id": "..." }`
  - records click for latest matching session
- `POST /evaluation/completion`
  - request: `{ "user_id": "...", "item_id": "..." }`
  - records completion for latest matching session
- `GET /evaluation/report?k=5`
  - returns aggregated evaluation report with all metrics

---

## 5) Reproducible Test Protocol

### Prerequisites

1. Backend running at `http://localhost:5235`
2. AI service running at `http://localhost:8001`
3. Valid user exists (example: `user-2`)

### Step A — Generate recommendations

```bash
curl -X POST "http://127.0.0.1:8001/generate/user-2"
```

Expected:
- `200 OK`
- one new session in `EVALUATION_LOG_FILE`

### Step B — Get one recommended item id

```bash
curl "http://localhost:5235/api/users/user-2/recommendations?limit=5"
```

Pick one `learningResourceId` as `RID`.

### Step C — Register click and completion

```bash
curl -X POST "http://127.0.0.1:8001/evaluation/click" \
  -H "Content-Type: application/json" \
  -d "{\"user_id\":\"user-2\",\"item_id\":\"RID\"}"
```

```bash
curl -X POST "http://127.0.0.1:8001/evaluation/completion" \
  -H "Content-Type: application/json" \
  -d "{\"user_id\":\"user-2\",\"item_id\":\"RID\"}"
```

### Step D — Read report

```bash
curl "http://127.0.0.1:8001/evaluation/report?k=5"
```

Expected trend:
- `ctr` increases after click events
- `completion_rate` increases after completion events
- `ndcg@k` is higher when relevant/completed items are ranked higher
- `coverage` increases as more distinct items are recommended over time
- `diversity` increases with more heterogeneous top-k lists
- `novelty` increases when long-tail (less popular) items are recommended

---

## 6) PowerShell Test Variant (Windows)

```powershell
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:8001/generate/user-2"

$recs = Invoke-RestMethod -Method Get -Uri "http://localhost:5235/api/users/user-2/recommendations?limit=5"
$rid = $recs[0].learningResourceId

Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:8001/evaluation/click" -ContentType "application/json" -Body (@{ user_id="user-2"; item_id=$rid } | ConvertTo-Json)
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:8001/evaluation/completion" -ContentType "application/json" -Body (@{ user_id="user-2"; item_id=$rid } | ConvertTo-Json)

Invoke-RestMethod -Method Get -Uri "http://127.0.0.1:8001/evaluation/report?k=5"
```

---

## 7) Interpretation Guide (Thesis-Friendly)

- **Precision/Recall**: relevance quality at cutoff `k`
- **nDCG**: ranking quality with graded relevance (completion > click)
- **CTR/Completion rate**: user engagement and educational outcome proxy
- **Coverage**: catalog utilization breadth
- **Diversity**: how different recommendations are in each list
- **Novelty**: how exploratory/long-tail recommendations are

Use metrics together; no single metric is sufficient.

---

## 8) Known Limitations and Next Improvements

- Current logs are local file-based (single-instance friendly, not distributed).
- Precision/Recall are binary relevance (click = completion in these two metrics).
- Diversity uses lightweight metadata features, not embedding-level semantic distance.
- Novelty uses interaction-frequency popularity; can be extended with time decay.

Potential next step:
- move logs to backend/database for production-grade analytics and dashboarding.

---

## 9) Thesis Positioning

This module is suitable for a bachelor thesis because it supports:
- **online evaluation from real runtime interactions**
- **ranking + behavioral + diversity/discovery perspectives**
- **clear, reproducible methodology and transparent formulas**
