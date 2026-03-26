# FE - Intelligent Educational System (Wireframe + Integration Spec)

This document locks in the agreed **frontend wireframe structure** and how each part uses the **backend APIs**.
It is meant to keep the UI consistent and scalable as we implement the remaining pages (tasks/scheduling, full recommendation filters, profile personalization).

## 1. Product Wireframe (what the user sees)

### 1.1. Global Navigation / Shell

- Left vertical sidebar with app name and route links.
- Top bar that shows the current page title + current user identifier.
- Main content area where each page renders its section(s).

### 1.2. Pages

#### A) Dashboard (AI Study Companion Dashboard)

Sections:

- Recommended for you
- Today’s plan (scheduler / “AI suggested schedule” placeholder until schedule APIs exist)
- Progres (basic): progress overview cards
- Quick stats: small KPI tiles

Backend data used:

- `GET /api/users/{id}/recommendations?limit=N` for “Recommended for you”
- `GET /api/users/{id}/analytics` for progress + quick stats (KPIs like completion rate, time spent, XP, tasks counts)
- (Optional later) `GET /api/users/{id}/mastery` for level / suggested difficulty that can drive “Today’s plan”

#### B) Recommendations Page (explanation-first)

UI concept:

- A grid/list of recommendation cards.
- Each card displays a clear “motivation/reason” to help the user decide.

Each card shows:

- title (resource title)
- dificultate (resource difficulty)
- durată (estimatedDurationMinutes)
- motiv recomandare (explanation text; MUST be visible)
- action CTA (e.g. “Start”)

Backend data used:

- `GET /api/users/{id}/recommendations?limit=N`
- CTA action tracking:
  - `POST /api/interactions` with:
    - `interactionType: "Viewed"` (when user starts the resource)
    - `learningResourceId: {resourceId}`
    - `timeSpentMinutes: 0` initially

#### C) Task & Scheduling (AI suggested schedule)

Wireframe requirements (from your thesis UX):

- task list
- calendar / timeline
- sugestii automate
- MUST HAVE: “AI suggested schedule”

Backend status note (important for correctness):

- **Currently there are no schedule/task REST endpoints implemented in backend**.
- Therefore the FE must treat the scheduler as a placeholder until backend exposes schedule/task APIs.

Planned FE integration approach (when APIs exist):

- Replace placeholder schedule items with backend read/write schedule payloads.

#### D) User Profile (Personalization)

Wireframe sections:

- preferințe
- nivel dificultate (based on mastery/suggestedDifficulty)
- statistici (KPIs)
- comportament (interaction-driven summary)

Backend data used:

- `GET /api/users/{id}` for profile + preferences + daily available minutes
- `GET /api/users/{id}/preferences` and `PUT /api/users/{id}/preferences` to edit preferences
- `GET /api/users/{id}/xp` (level + xp)
- `GET /api/users/{id}/mastery` for `suggestedDifficulty` + `suggestedDifficultyReason`
- (Optional for “comportament” once UI is implemented) `GET /api/users/{id}/interactions`

## 2. Conceptual UX Flow (how user makes decisions)

### 2.1. How recommendations are received

1. User opens Dashboard or Recommendations page.
2. FE calls `GET /api/users/{id}/recommendations?limit=N`.
3. FE renders each recommendation card with:
  - resource metadata (title, difficulty, duration)
  - explanation (reason)
  - score + algorithmUsed (optional visual later)

### 2.2. How user interacts with the system

1. On a recommendation card, user clicks **Start**.
2. FE records interaction:
  - `POST /api/interactions` (interactionType `"Viewed"`)
3. FE optionally later triggers UI updates (e.g. remove card or refresh recommendations).

### 2.3. How the system adapts decisions (next iteration)

1. Backend analytics/mastery incorporate user interactions.
2. FE reads:
  - `GET /api/users/{id}/analytics` (progress/KPIs)
  - `GET /api/users/{id}/mastery` (suggestedDifficulty)
3. FE uses these values to influence what “Today’s plan” and UI priorities should be.

## 3. Design System (colors + clean aesthetic)

Theme colors (wireframe requirement):

- Blue: AI / education
- Green: progress
- Yellow: recommendations

Implementation rule:

- Prefer consistent CSS variables in `frontend/src/index.css` and reuse them across components.
- Avoid hardcoded colors inside components; only use variables.

## 4. Frontend Implementation Rules (scalability/correctness)

### 4.1. Layering

- `frontend/src/services/api.ts` is the only place that should call `fetch()` directly.
- Each API call returns strongly typed promises matching backend DTOs.
- `frontend/src/hooks/`*:
  - call services
  - manage loading/error state
  - cancel stale requests when dependencies change
- `frontend/src/pages/*`:
  - compose UI sections for each route
- `frontend/src/components/*`:
  - presentational components only (should not call backend directly)

### 4.2. User identity

- User id must come from FE “current user” state (context/session).
- No hardcoded `USER_ID` in components.

Current FE behavior:

- `frontend/src/context/UserContext` provides a default `userId` (until auth is implemented).

## 5. Backend Contract Mapping (exact endpoints used by current FE)

These endpoints MUST remain aligned with backend DTOs:

- Recommendations:
  - `GET /api/users/{id}/recommendations?limit=N`
  - Response item:
    - `recommendationId`
    - `resource`: `{ id, title, description?, topic, difficulty, estimatedDurationMinutes, contentType }`
    - `score`, `algorithmUsed`, `explanation`, `createdAtUtc`
- Interaction tracking:
  - `POST /api/interactions`
  - Request payload:
    - `userId`
    - `learningResourceId`
    - `interactionType` in `{ Viewed, Completed, Rated, Skipped }`
    - optional `rating` (1..5) and `timeSpentMinutes` (>= 0)
- Profile:
  - `GET /api/users/{id}` and `GET/PUT /api/users/{id}/preferences`
- Progress/Quick stats:
  - `GET /api/users/{id}/analytics`
- Mastery (level difficulty):
  - `GET /api/users/{id}/mastery`

## 6. Current Implementation Scope (what is working now vs placeholder)

Working (connected correctly to backend APIs today):

- Dashboard renders:
  - “Recommended for you” using `useRecommendations` + `RecommendationCard`
  - Recommendation CTA writes an interaction using `POST /api/interactions`
- Existing shell (sidebar/topbar/layout) is wired with a shared user context.

Placeholder / not yet integrated (expected next):

- Tasks & Scheduling (calendar/timeline/suggested schedule)
- Profile sections beyond placeholder pages

## 7. Next steps for consistency

When you implement remaining components:

- Follow the section/component contracts in this file.
- Keep backend mapping stable (endpoint + payload fields).
- Re-run `npm run build` and `npm run lint` after each integration step.

