# Backend API — Guide for the Frontend

This document helps the **frontend** team integrate with the Educational Companion backend: which endpoints to use for each feature and what response shapes to expect.

---

## 1. Base URL and protocol

- **Backend base URL:** `http://localhost:5235` (development). All paths below are relative to this base.
- **JSON** for request and response bodies. Send `Content-Type: application/json` when posting/putting.
- **User id** in paths is a **string** (e.g. from your auth: `"user-1"`). Resource and interaction ids are **UUIDs**.

---

## 2. Endpoints by feature

### User profile and settings

| Feature | Method | Path | Notes |
|--------|--------|------|--------|
| Dashboard / profile | GET | `/api/users/{id}` | Full profile + preferences (level, XP, daily minutes, preferred difficulty, content types, topics). |
| Settings — load | GET | `/api/users/{id}/preferences` | Only preferences. |
| Settings — save | PUT | `/api/users/{id}/preferences` | Body: `{ preferredDifficulty?, preferredContentTypesCsv?, preferredTopicsCsv? }`. 204 on success. |
| XP / level display | GET | `/api/users/{id}/xp` | `{ userId, level, xp }`. |

Use the same `{id}` as the logged-in user (when auth is in place).

---

### Learning resources (catalog and detail)

| Feature | Method | Path | Notes |
|--------|--------|------|--------|
| Catalog / browse | GET | `/api/resources` | Optional query: `topic`, `difficulty` (1–5), `contentType` (Article, Video, Quiz). Returns array of resources. |
| Resource detail | GET | `/api/resources/{id}` | Single resource (id, title, description, topic, difficulty, estimatedDurationMinutes, contentType). |
| Admin: create resource | POST | `/api/resources` | Body: title, description?, topic, difficulty, estimatedDurationMinutes, contentType. 201 + Location. |
| Admin: update resource | PUT | `/api/resources/{id}` | Same body shape. 204. |
| Admin: delete resource | DELETE | `/api/resources/{id}` | 204. |

**Resource object:** `id`, `title`, `description`, `topic`, `difficulty` (1–5), `estimatedDurationMinutes`, `contentType` (Article | Video | Quiz).

---

### User interactions (track views, completions, ratings)

| Feature | Method | Path | Notes |
|--------|--------|------|--------|
| User’s history | GET | `/api/users/{id}/interactions` | Or `GET /api/interactions/by-user/{userId}`. Ordered by recent first. |
| Record view / completion / rating | POST | `/api/interactions` | Body: `userId`, `learningResourceId`, `interactionType` (Viewed, Completed, Rated, Skipped), optional `rating` (1–5), optional `timeSpentMinutes`. 201 + created interaction. |
| Update interaction | PUT | `/api/interactions/{id}` | e.g. add rating or time spent. 204. |
| Delete interaction | DELETE | `/api/interactions/{id}` | 204. |

**Interaction object:** `id`, `userId`, `learningResourceId`, `interactionType`, `rating`, `timeSpentMinutes`, `createdAtUtc`.

---

### Recommendations (personalized content list)

| Feature | Method | Path | Notes |
|--------|--------|------|--------|
| Recommendations list | GET | `/api/users/{id}/recommendations` | Optional `?limit=10`. Returns array of items: recommendation id, full **resource** object, **score**, **algorithmUsed**, **explanation**, **createdAtUtc**. Use for “For you” / “Recommended” section. |

**Do not call** `POST /api/users/{id}/recommendations` from the frontend; that is for the AI service to write recommendations.

---

### Analytics and mastery (EDM — dashboards and adaptivity)

| Feature | Method | Path | Notes |
|--------|--------|------|--------|
| Analytics / KPIs | GET | `/api/users/{id}/analytics` | Summary text + KPIs: viewed/completed counts, completion rate, average rating, time spent, XP, level, task counts, gamification events count. Use for dashboard or stats. |
| Topic mastery & suggested difficulty | GET | `/api/users/{id}/mastery` | Per-topic: topic name, resources completed, average rating, average difficulty, mastery level (None/Beginner/Intermediate/Advanced). Plus one **suggestedDifficulty** (1–5) and a short reason. Use for “Suggested level” or topic-strength UI. |

---

## 3. Response shapes (quick reference)

- **Profile:** `userId`, `level`, `xp`, `dailyAvailableMinutes`, `preferences` (object with preferredDifficulty, preferredContentTypesCsv, preferredTopicsCsv).
- **Preferences:** same `preferences` object only.
- **XP:** `userId`, `level`, `xp`.
- **Interactions:** array of `{ id, userId, learningResourceId, interactionType, rating?, timeSpentMinutes?, createdAtUtc }`.
- **Resources:** array of `{ id, title, description, topic, difficulty, estimatedDurationMinutes, contentType }`.
- **Recommendations:** array of `{ recommendationId, resource, score, algorithmUsed, explanation, createdAtUtc }` where `resource` is the same shape as a single resource.
- **Analytics:** `userId`, `summary` (summaryText, computedAtUtc), `kpis` (all numeric/count fields listed in API-Reference).
- **Mastery:** `userId`, `topicMastery` (array of topic objects + masteryLevel), `suggestedDifficulty`, `suggestedDifficultyReason`, `computedAtUtc`.

Exact field names and types are in **[API-Reference.md](./API-Reference.md)** (including nested objects and enums).

---

## 4. Error handling

- **404**: User or resource or interaction not found — show a “not found” or redirect.
- **400**: Validation (e.g. invalid difficulty, missing required field) — show validation message from response if available.
- **500**: Server error — show generic error message.

The backend uses exception middleware; response body format may be message string or problem-details. Prefer checking **status code** and handling 4xx/5xx accordingly.

---

## 5. Auth (future)

Endpoints are currently **unauthenticated**. When auth is added, the backend will likely expect a token (e.g. Bearer) and derive the current user id; path `{id}` for user-scoped routes may then be fixed to “current user” or validated against the token. Until then, the frontend can pass the chosen user id in the path.

---

## 6. Full API and thesis docs

- **All endpoints and contracts:** [API-Reference.md](./API-Reference.md)
- **EDM (analytics, mastery, data sources):** [EDM-Layer.md](./EDM-Layer.md)
- **Backend overview and what’s implemented:** [README.md](./README.md)
