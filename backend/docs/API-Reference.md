# API Reference

Complete reference for the Educational Companion backend API. Use this for **integration (AI service, frontend)** and **development**. **Backend base URL:** `http://localhost:5235` (development). All paths below are relative to this base. All request/response bodies are JSON unless noted.

**General:**

- **Content-Type**: `application/json` for request bodies.
- **Status codes**: 200 OK, 201 Created, 204 No Content, 400 Bad Request, 404 Not Found, 409 Conflict, 500 Internal Server Error (see exception middleware).
- **Ids**: User ids are **strings** (e.g. `"user-1"`). Resource, interaction, and recommendation ids are **UUIDs** (e.g. `"3fa85f64-5717-4562-b3fc-2c963f66afa6"`).

---

## 1. Users — `api/users`

Base route: `api/users`. User id in path is a **string** (`{id}`).

### GET `/api/users/{id}`

Returns the full user profile including preferences.

**Response 200:** `UserProfileResponse`

```json
{
  "userId": "user-1",
  "level": 2,
  "xp": 150,
  "dailyAvailableMinutes": 60,
  "preferences": {
    "preferredDifficulty": 2,
    "preferredContentTypesCsv": "Article,Video",
    "preferredTopicsCsv": "Python,Web"
  }
}
```

**Errors:** 404 if user not found.

---

### GET `/api/users/{id}/preferences`

Returns only the user’s preferences.

**Response 200:** `UserPreferencesResponse`

```json
{
  "preferredDifficulty": 2,
  "preferredContentTypesCsv": "Article,Video",
  "preferredTopicsCsv": "Python,Web"
}
```

**Errors:** 404 if user not found.

---

### PUT `/api/users/{id}/preferences`

Updates only preferences. Does not change XP or level.

**Request body:** `UpdateUserPreferencesRequest`

```json
{
  "preferredDifficulty": 2,
  "preferredContentTypesCsv": "Article,Video",
  "preferredTopicsCsv": "Python,Web"
}
```

- All fields optional. `preferredDifficulty`: 1–5. Max lengths: content types 200, topics 500.

**Response:** 204 No Content.

**Errors:** 404 user not found; 400 validation (e.g. difficulty out of range).

---

### GET `/api/users/{id}/xp`

Returns XP and level only.

**Response 200:** `UserXpResponse`

```json
{
  "userId": "user-1",
  "level": 2,
  "xp": 150
}
```

**Errors:** 404 if user not found.

---

### GET `/api/users/{id}/interactions`

Returns all interactions for the user (most recent first).

**Response 200:** array of `UserInteractionResponse`

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "userId": "user-1",
    "learningResourceId": "3fa85f64-5717-4562-b3fc-2c963f66afa7",
    "interactionType": "Completed",
    "rating": 5,
    "timeSpentMinutes": 25,
    "createdAtUtc": "2025-02-26T12:00:00Z"
  }
]
```

**InteractionType:** `Viewed` | `Completed` | `Rated` | `Skipped`.

**Errors:** 404 if user not found.

---

### GET `/api/users/{id}/analytics` (EDM)

User analytics: short summary text and KPIs (for dashboards/reporting).

**Response 200:** `UserAnalyticsResponse`

```json
{
  "userId": "user-1",
  "summary": {
    "summaryText": "5 of 10 viewed resources completed (50% completion rate). Total study time: 120 minutes.",
    "computedAtUtc": "2025-02-26T12:00:00Z"
  },
  "kpis": {
    "totalResourcesViewed": 10,
    "totalResourcesCompleted": 5,
    "completionRatePercent": 50.0,
    "averageRatingGiven": 4.2,
    "totalTimeSpentMinutes": 120,
    "totalXpEarned": 150,
    "currentLevel": 2,
    "tasksCompleted": 3,
    "tasksPending": 2,
    "tasksOverdue": 0,
    "gamificationEventsCount": 8
  }
}
```

**Errors:** 404 if user not found.

---

### GET `/api/users/{id}/recommendations` (EDM — read)

Personalized recommendations for the user (content list with score and explanation). Ordered by score descending.

**Query parameters:**

| Name   | Type  | Required | Description        |
|--------|-------|----------|--------------------|
| `limit`| int   | No       | Max number to return (e.g. 10) |

**Response 200:** array of `UserRecommendationItemResponse`

```json
[
  {
    "recommendationId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
    "resource": {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa7",
      "title": "Python Data Structures",
      "description": "...",
      "topic": "Python",
      "difficulty": 2,
      "estimatedDurationMinutes": 30,
      "contentType": "Article"
    },
    "score": 0.92,
    "algorithmUsed": "Hybrid",
    "explanation": "Content + collaborative score; matches your Python interest.",
    "createdAtUtc": "2025-02-26T12:00:00Z"
  }
]
```

**ContentType:** `Article` | `Video` | `Quiz`.

**Errors:** 404 if user not found.

---

### POST `/api/users/{id}/recommendations` (Recommendation write — AI service)

Creates or replaces recommendations for the user. Used by the AI service to persist hybrid/content-based/collaborative results.

**Request body:** `CreateRecommendationsBatchRequest`

```json
{
  "recommendations": [
    {
      "learningResourceId": "3fa85f64-5717-4562-b3fc-2c963f66afa7",
      "score": 0.92,
      "algorithmUsed": "Hybrid",
      "explanation": "Content + collaborative score; matches your Python interest."
    }
  ],
  "replaceExisting": true
}
```

- **recommendations**: array of at least one item. **score**: 0.0–1.0. **algorithmUsed**: required, max 50 chars. **explanation**: required, max 1000 chars.
- **replaceExisting**: if `true`, all existing recommendations for this user are deleted before inserting the new batch; if `false`, new items are appended.

**Response 201 Created:** `CreatedRecommendationsResponse`

```json
{
  "userId": "user-1",
  "createdCount": 5,
  "replacedExisting": true
}
```

**Headers:** `Location: GET /api/users/{id}/recommendations`.

**Errors:** 400 validation (empty list, invalid score, missing/long fields); 404 user or learning resource not found.

---

### GET `/api/users/{id}/mastery` (EDM)

Topic-level mastery and a single suggested difficulty (1–5) for adaptive learning.

**Response 200:** `UserMasteryResponse`

```json
{
  "userId": "user-1",
  "topicMastery": [
    {
      "topic": "Python",
      "resourcesCompleted": 5,
      "averageRating": 4.2,
      "averageDifficultyCompleted": 2.0,
      "masteryLevel": "Advanced"
    }
  ],
  "suggestedDifficulty": 3,
  "suggestedDifficultyReason": "Based on 2 topic(s); average completed difficulty 2.0, rating 4.2. Suggested next: 3.",
  "computedAtUtc": "2025-02-26T12:00:00Z"
}
```

**masteryLevel:** `None` | `Beginner` | `Intermediate` | `Advanced`.

**Errors:** 404 if user not found.

---

## 2. Learning resources — `api/resources`

Base route: **`api/resources`** (LearningResourcesController). Resource id in path is a **UUID** (`{id}`).

### GET `/api/resources`

Returns all resources, or filtered list when query parameters are provided.

**Query parameters:**

| Name          | Type  | Required | Description                          |
|---------------|-------|----------|--------------------------------------|
| `topic`       | string| No       | Exact match (case-insensitive)        |
| `difficulty`  | int   | No       | 1–5                                   |
| `contentType` | string| No       | `Article` \| `Video` \| `Quiz`        |

If all are omitted, returns all resources.

**Response 200:** array of `LearningResourceResponse`

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa7",
    "title": "Python Data Structures",
    "description": "...",
    "topic": "Python",
    "difficulty": 2,
    "estimatedDurationMinutes": 30,
    "contentType": "Article"
  }
]
```

---

### GET `/api/resources/{id}`

Returns a single resource by id.

**Response 200:** `LearningResourceResponse` (same shape as above).

**Errors:** 404 if resource not found.

---

### POST `/api/resources`

Creates a new learning resource.

**Request body:** `CreateLearningResourceRequest`

```json
{
  "title": "Python Data Structures",
  "description": "Optional description.",
  "topic": "Python",
  "difficulty": 2,
  "estimatedDurationMinutes": 30,
  "contentType": "Article"
}
```

- **title**: required, 1–200 chars. **topic**: required, 1–100 chars. **difficulty**: 1–5. **estimatedDurationMinutes**: 1–9999. **contentType**: required, max 50 chars (e.g. `Article`, `Video`, `Quiz`).

**Response 201 Created:** `LearningResourceResponse`. **Location** header points to `GET /api/resources/{id}`.

**Errors:** 400 validation.

---

### PUT `/api/resources/{id}`

Updates an existing resource. Same body shape as create (use `UpdateLearningResourceRequest`; typically same fields, all optional for update in many implementations).

**Response:** 204 No Content.

**Errors:** 404 resource not found; 400 validation.

---

### DELETE `/api/resources/{id}`

Deletes a resource.

**Response:** 204 No Content.

**Errors:** 404 if resource not found.

---

## 3. User interactions — `api/interactions`

Base route: **`api/interactions`** (UserInteractionsController). Interaction id in path is a **UUID** (`{id}`).

### GET `/api/interactions`

Returns all interactions, or filtered when any query parameter is provided.

**Query parameters:**

| Name               | Type  | Required | Description                    |
|--------------------|-------|----------|--------------------------------|
| `userId`           | string| No       | Filter by user                 |
| `learningResourceId` | Guid | No     | Filter by resource             |
| `interactionType`  | string| No       | `Viewed` \| `Completed` \| `Rated` \| `Skipped` |

**Response 200:** array of `UserInteractionResponse` (same shape as in Users section).

---

### GET `/api/interactions/by-user/{userId}`

Returns all interactions for the given user (string id). Same response shape as `GET /api/users/{id}/interactions`.

**Response 200:** array of `UserInteractionResponse`.

---

### GET `/api/interactions/{id}`

Returns one interaction by id.

**Response 200:** `UserInteractionResponse`.

**Errors:** 404 if not found.

---

### POST `/api/interactions`

Creates a new interaction (e.g. user viewed or completed a resource).

**Request body:** `CreateUserInteractionRequest`

```json
{
  "userId": "user-1",
  "learningResourceId": "3fa85f64-5717-4562-b3fc-2c963f66afa7",
  "interactionType": "Completed",
  "rating": 5,
  "timeSpentMinutes": 25
}
```

- **userId**: required, 1–450 chars. **learningResourceId**: required. **interactionType**: required, max 50 chars (`Viewed`, `Completed`, `Rated`, `Skipped`). **rating**: optional, 1–5 (required when type is `Rated`). **timeSpentMinutes**: optional, 0–9999.

**Response 201 Created:** `UserInteractionResponse`. **Location** points to `GET /api/interactions/{id}`.

**Errors:** 400 validation; 404 if learning resource not found.

---

### PUT `/api/interactions/{id}`

Updates an interaction (e.g. add rating or time spent). Body: `UpdateUserInteractionRequest` (interactionType, rating, timeSpentMinutes — typically all optional).

**Response:** 204 No Content.

**Errors:** 404 not found; 400 validation (e.g. rating 1–5).

---

### DELETE `/api/interactions/{id}`

Deletes an interaction.

**Response:** 204 No Content.

**Errors:** 404 if not found.

---

## Summary table

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/users/{id}` | Profile + preferences |
| GET | `/api/users/{id}/preferences` | Preferences only |
| PUT | `/api/users/{id}/preferences` | Update preferences |
| GET | `/api/users/{id}/xp` | XP and level |
| GET | `/api/users/{id}/interactions` | User’s interactions |
| GET | `/api/users/{id}/analytics` | EDM analytics |
| GET | `/api/users/{id}/recommendations` | EDM recommendations (read) |
| POST | `/api/users/{id}/recommendations` | Write recommendations (AI) |
| GET | `/api/users/{id}/mastery` | EDM mastery |
| GET | `/api/resources` | List/search resources |
| GET | `/api/resources/{id}` | One resource |
| POST | `/api/resources` | Create resource |
| PUT | `/api/resources/{id}` | Update resource |
| DELETE | `/api/resources/{id}` | Delete resource |
| GET | `/api/interactions` | List/search interactions |
| GET | `/api/interactions/by-user/{userId}` | Interactions by user |
| GET | `/api/interactions/{id}` | One interaction |
| POST | `/api/interactions` | Create interaction |
| PUT | `/api/interactions/{id}` | Update interaction |
| DELETE | `/api/interactions/{id}` | Delete interaction |

---

## Error responses

The API uses custom exceptions mapped by middleware:

- **400 Bad Request**: Validation (invalid range, missing required field, etc.).
- **404 Not Found**: User, resource, interaction, or recommendation not found (entity-specific exception).
- **409 Conflict**: Business conflict (e.g. duplicate, already completed).
- **500 Internal Server Error**: Recommendation generation failure, gamification rule violation, or unhandled exception.

Error response body shape may vary; typically a message or problem-details style. Check middleware implementation for the exact format.
