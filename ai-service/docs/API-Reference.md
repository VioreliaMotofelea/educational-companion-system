# AI Service — API Reference

This document describes the **AI service** HTTP API: recommendation generation and evaluation endpoints. It is intended for **backend** and **frontend** developers who call the AI service.

**Base URL (development):** `http://localhost:8001`. All paths below are relative to this base. Responses are JSON unless noted.

---

## Endpoint

### POST `/generate/{user_id}`

Generates hybrid recommendations for the given user and **persists them to the backend**. The AI service fetches user data, interactions, resources, and mastery from the backend; runs the hybrid algorithm (content-based + collaborative + EDM difficulty); then posts the top-N recommendations to the backend (replacing any existing recommendations for that user).

**Path parameter:**

| Name     | Type   | Required | Description        |
|----------|--------|----------|--------------------|
| `user_id` | string | Yes      | User identifier (e.g. `user-1`, `user-2`). Must exist in the backend. |

**Request body:** None.

**Response 200 OK:** `RecommendationGenerationResponse`

```json
{
  "userId": "user-2",
  "generated": 10,
  "backendResponse": {
    "userId": "user-2",
    "createdCount": 10,
    "replacedExisting": true
  }
}
```

| Field              | Type   | Description |
|--------------------|--------|-------------|
| `userId`           | string | User for whom recommendations were generated. |
| `generated`        | int    | Number of recommendations generated. |
| `backendResponse`  | object | Result of writing to the backend. |
| `backendResponse.userId` | string | Same as `userId`. |
| `backendResponse.createdCount` | int | Number of recommendations stored in the backend. |
| `backendResponse.replacedExisting` | bool | Whether previous recommendations for this user were replaced. |

**Error responses**

| Status | Meaning |
|--------|--------|
| **404** | User not found in the backend (e.g. invalid `user_id`). Body: `{"detail": "..."}`. |
| **502** | Backend unavailable, timeout, or backend returned an error. Body: `{"detail": "..."}`. |
| **504** | Backend request timed out. Body: `{"detail": "Backend request timed out."}`. |
| **500** | Unexpected internal error in the AI service (e.g. recommendation logic failure). Body: `{"detail": "An unexpected internal server error occurred."}`. |

Validation errors (e.g. invalid request) return **422** with a structured `detail` array (FastAPI default).

---

## Evaluation Endpoints

### POST `/evaluation/click`

Registers a click event for a recommended item in the latest matching recommendation session.

**Request body**

```json
{
  "user_id": "user-2",
  "item_id": "resource-uuid"
}
```

**Response 200 OK**

```json
{
  "message": "Click event recorded.",
  "user_id": "user-2",
  "item_id": "resource-uuid"
}
```

Returns **404** if no recommendation session exists for that `(user_id, item_id)`.

### POST `/evaluation/completion`

Registers a completion event for a recommended item in the latest matching recommendation session.

Same request/response shape as `/evaluation/click` with message `"Completion event recorded."`.

### GET `/evaluation/report?k=5`

Computes online evaluation metrics from persisted logs:
- `precision@k`
- `recall@k`
- `ndcg@k`
- `ctr`
- `completion_rate`
- `coverage`
- `diversity`
- `novelty`

**Response 200 OK**

```json
{
  "k": 5,
  "logs_count": 24,
  "precision_at_k": 0.31,
  "recall_at_k": 0.28,
  "ndcg_at_k": 0.34,
  "ctr": 0.19,
  "completion_rate": 0.12,
  "coverage": 0.44,
  "diversity": 0.61,
  "novelty": 0.48
}
```

---

## Health / root

### GET `/`

Returns a simple status payload. No authentication.

**Response 200 OK:**

```json
{
  "service": "Educational Companion AI",
  "status": "running"
}
```

---

## OpenAPI / Swagger

When the service is running, interactive documentation is available at:

- **Swagger UI:** `http://localhost:8001/docs`
- **OpenAPI JSON:** `http://localhost:8001/openapi.json`

Use these to inspect the exact request/response schemas and to test the API.
