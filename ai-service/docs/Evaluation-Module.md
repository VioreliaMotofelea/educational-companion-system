# AI Service — Evaluation Module

This document explains how the evaluation module is implemented and integrated in the AI service, and how to test that metrics are computed correctly.

---

## Purpose

The evaluation module measures recommendation quality after hybrid recommendations are generated and user behavior events are recorded.

It supports:
- **Ranking metrics**: `precision@k`, `recall@k`, `ndcg@k`
- **Behavioral metrics**: `ctr`, `completion_rate`

---

## Implementation Overview

### 1) Log model and persistence

- File: `evaluation/tracking.py`
- Config path: `config.py` -> `EVALUATION_LOG_FILE`
- Stored JSON object per recommendation session:

```json
{
  "user_id": "user-2",
  "recommended_items": ["id1", "id2", "id3"],
  "clicked_items": [],
  "completed_items": []
}
```

Flow:
- After `POST /generate/{user_id}`, the service appends a new session log.
- Click/completion events update the latest matching session for `(user_id, item_id)`.

### 2) Metric calculation

- File: `evaluation/metrics.py`
- Aggregation: `evaluation/evaluator.py`

Relevance mapping used by evaluator:
- clicked item -> relevance `1`
- completed item -> relevance `2` (highest)

Computed metrics:
- `precision@k`: relevant in top-k / k
- `recall@k`: relevant in top-k / total relevant
- `ndcg@k`: ranked gain with multi-level relevance (click vs completion)
- `ctr`: total clicked / total recommended
- `completion_rate`: total completed / total recommended

### 3) API integration

- File: `api/routes.py`

Endpoints:
- `POST /generate/{user_id}`
  - generates recommendations
  - pushes recommendations to backend
  - appends evaluation session log
- `POST /evaluation/click`
  - body: `{ "user_id": "...", "item_id": "..." }`
  - records click on a recommended item
- `POST /evaluation/completion`
  - body: `{ "user_id": "...", "item_id": "..." }`
  - records completion on a recommended item
- `GET /evaluation/report?k=5`
  - returns aggregated metrics from log file

---

## How to Test End-to-End

Use this sequence to verify that evaluation metrics really work.

### Prerequisites

1. Backend running at `http://localhost:5235`
2. AI service running at `http://localhost:8001`
3. At least one valid user (for example `user-2`)

### Step 1: Generate recommendations (creates session log)

Call:

```bash
curl -X POST "http://127.0.0.1:8001/generate/user-2"
```

Expected:
- `200 OK`
- response includes `generated` and backend confirmation
- a new session is appended to `EVALUATION_LOG_FILE`

### Step 2: Get generated item ids

Call backend recommendations:

```bash
curl "http://localhost:5235/api/users/user-2/recommendations?limit=5"
```

Pick one `learningResourceId` from the response (call it `RID`).

### Step 3: Register a click

```bash
curl -X POST "http://127.0.0.1:8001/evaluation/click" \
  -H "Content-Type: application/json" \
  -d "{\"user_id\":\"user-2\",\"item_id\":\"RID\"}"
```

Expected:
- `200 OK`
- message: `"Click event recorded."`

### Step 4: Register a completion

```bash
curl -X POST "http://127.0.0.1:8001/evaluation/completion" \
  -H "Content-Type: application/json" \
  -d "{\"user_id\":\"user-2\",\"item_id\":\"RID\"}"
```

Expected:
- `200 OK`
- message: `"Completion event recorded."`

### Step 5: Read evaluation report

```bash
curl "http://127.0.0.1:8001/evaluation/report?k=5"
```

Expected:
- `logs_count` >= 1
- `ctr` > 0 after click registration
- `completion_rate` > 0 after completion registration
- `precision_at_k`, `recall_at_k`, and `ndcg_at_k` reflect top-k ranking quality

---

## Quick Validation Rules

To validate correctness quickly:
- If no click/completion was recorded, `ctr` and `completion_rate` should be `0`.
- Adding one click should increase `ctr`.
- Adding one completion should increase `completion_rate`.
- Completing a high-ranked item should improve `ndcg@k` more than completing a low-ranked item.

---

## Notes for Thesis

- This module currently performs **online-style log-based evaluation** from real recommendation sessions and events.
- Offline experiments can be done with `evaluation/offline_eval.py` once a final dataset is selected.
- The current setup is appropriate for demonstrating both:
  - algorithmic ranking quality (`precision@k`, `recall@k`, `ndcg@k`)
  - behavioral impact (`ctr`, `completion_rate`)
