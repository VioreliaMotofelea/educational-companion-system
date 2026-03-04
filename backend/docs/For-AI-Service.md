# Backend API — Guide for the AI Service (Python)

This document is for the **AI service** (Python) that implements hybrid/content-based/collaborative recommendations. It summarizes which backend endpoints to call and the exact contract for **writing** recommendations.

---

## 1. Base URL and protocol

- Use the backend base URL (e.g. `http://localhost:5000` or your deployed URL). All endpoints are relative to this.
- Send **JSON** request bodies with `Content-Type: application/json`.
- User ids in paths are **strings** (e.g. `user-1`). Resource and interaction ids are **UUIDs**.

---

## 2. Data you need from the backend (read)

To compute recommendations for a user (or for all users), the AI service typically needs:

| Data | Endpoint | Notes |
|------|----------|--------|
| User profile + preferences | `GET /api/users/{userId}` | Level, XP, daily minutes, preferred difficulty, content types, topics (CSV) |
| User’s interactions | `GET /api/users/{userId}/interactions` | Or `GET /api/interactions?userId={userId}` or `GET /api/interactions/by-user/{userId}`. Use for history (viewed, completed, rated, time spent). |
| Full resource catalog | `GET /api/resources` | All learning resources (id, title, topic, difficulty, duration, contentType). Filter with `?topic=...&difficulty=...&contentType=...` if needed. |
| Optional: EDM mastery | `GET /api/users/{userId}/mastery` | Per-topic mastery and suggested difficulty; useful to bias difficulty of recommended resources. |

**Getting user ids:** The backend does **not** currently expose `GET /api/users` (list all users). If your AI runs in batch over all users, you must either (a) get the list of user ids from your own config/DB, or (b) ask the backend team to add `GET /api/users` returning minimal user list (e.g. ids). For a single-user flow (e.g. on-demand recommendation), the frontend or gateway provides the `userId`.

---

## 3. Writing recommendations (main integration point)

After computing recommended items for a user, the AI service **writes** them to the backend so the app (and EDM) can serve them with explanations.

### Endpoint

```
POST /api/users/{userId}/recommendations
```

Replace `{userId}` with the actual user id (string).

### Request body

```json
{
  "recommendations": [
    {
      "learningResourceId": "uuid-of-learning-resource",
      "score": 0.92,
      "algorithmUsed": "Hybrid",
      "explanation": "Short human-readable explanation for the user."
    }
  ],
  "replaceExisting": true
}
```

**Fields:**

- **recommendations** (array, required): At least one item. Each item has:
  - **learningResourceId** (UUID): Must be an existing resource id (from `GET /api/resources`).
  - **score** (number): 0.0–1.0 (e.g. relevance or confidence).
  - **algorithmUsed** (string, required): Max 50 characters (e.g. `"Hybrid"`, `"ContentBased"`, `"CollaborativeFiltering"`).
  - **explanation** (string, required): Max 1000 characters. Shown to the user (explainability).
- **replaceExisting** (boolean, optional, default `true`):  
  - `true`: Delete all existing recommendations for this user, then insert the new list (typical “refresh top-N” flow).  
  - `false`: Append to existing recommendations (no delete).

### Response

- **201 Created** with body:

```json
{
  "userId": "user-1",
  "createdCount": 5,
  "replacedExisting": true
}
```

- **Location** header: `GET /api/users/{userId}/recommendations` (so the client can read the new list).

### Errors

- **400 Bad Request**: Empty list, score out of [0, 1], missing or too-long `algorithmUsed`/`explanation`.
- **404 Not Found**: User does not exist, or one of the `learningResourceId` values is not a valid existing resource.

On success, the existing **read** endpoint `GET /api/users/{userId}/recommendations` (and optional `?limit=N`) will return the new recommendations with full resource details and your explanations.

---

## 4. Typical flow (hybrid recommender)

1. **Input:** Decide for which user(s) to run (e.g. one user id from request, or a list from your own source).
2. **Read:** For each user, call:
   - `GET /api/users/{userId}` (profile + preferences),
   - `GET /api/users/{userId}/interactions` (history),
   - `GET /api/resources` (catalog).
3. **Compute:** Run your hybrid (or content-based/collaborative) algorithm. Produce a list of (resource id, score, algorithm name, explanation) per user.
4. **Write:** For each user, call `POST /api/users/{userId}/recommendations` with `replaceExisting: true` and the list of recommendations.
5. **Optional:** Frontend or another service can then call `GET /api/users/{userId}/recommendations` to show the list with explanations.

---

## 5. Full API details

For all request/response shapes, query parameters, and error handling, use **[API-Reference.md](./API-Reference.md)**. For EDM concepts (data sources, KPIs, mastery), see **[EDM-Layer.md](./EDM-Layer.md)**.
