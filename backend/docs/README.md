# Educational Companion — Backend Documentation

This folder contains documentation for the **Educational Companion** backend API. It supports **thesis writing**, **further development**, integration with the **AI service (Python)**, and the **frontend** application.

---

## Purpose of the backend

The backend is the central API for the Educational Companion system. It provides:

- **User management**: profiles, preferences, XP/level (gamification data)
- **Learning resources**: catalog and search (topic, difficulty, content type)
- **User interactions**: track views, completions, ratings, time spent
- **Recommendations**: read (for users) and **write** (for the AI service)
- **Educational Data Mining (EDM)**: analytics, mastery, suggested difficulty

The AI service (Python) uses the backend to **read** user/interaction/resource data and **write** recommendations. The frontend uses the backend for all user-facing data and for displaying recommendations and analytics.

---

## Tech stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 8 |
| API | ASP.NET Core Web API |
| ORM | Entity Framework Core 8 |
| Database | PostgreSQL |
| Documentation | Swagger/OpenAPI (in Development) |

---

## Architecture

The solution follows **Clean Architecture** with three projects:

| Project | Role |
|---------|------|
| **EducationalCompanion.Domain** | Entities, enums, exceptions (no dependencies) |
| **EducationalCompanion.Infrastructure** | Persistence (EF Core, PostgreSQL), repositories, EDM read queries |
| **EducationalCompanion.Api** | Controllers, services, DTOs, middleware |

- **Controllers** expose REST endpoints.
- **Services** contain business logic and call **repositories** for data.
- **Repositories** abstract the database (DbContext).

---

## What is implemented (current state)

### Users and profile

- Get profile (with preferences), get/update preferences, get XP/level.
- All under `GET/PUT /api/users/{id}/...`.

### Learning resources

- Full CRUD and search by topic, difficulty, content type.
- Base path: `/api/resources`.

### User interactions

- Create, read, update, delete interactions (viewed, completed, rated, skipped).
- Search by user, resource, or interaction type.
- Base path: `/api/interactions`; also `GET /api/users/{id}/interactions`.

### Educational Data Mining (EDM) layer

- **Analytics**: `GET /api/users/{id}/analytics` — summary + KPIs (completion rate, time spent, XP, tasks, etc.).
- **Recommendations (read)**: `GET /api/users/{id}/recommendations?limit=N` — personalized content list with score and explanation.
- **Mastery**: `GET /api/users/{id}/mastery` — per-topic mastery and suggested difficulty (1–5).

See **[EDM-Layer.md](./EDM-Layer.md)** for data sources, KPI definitions, and mastery rules.

### Recommendation write API (for AI service)

- **Create/replace recommendations**: `POST /api/users/{id}/recommendations` with a batch (and optional replace-all).
- Used by the Python AI service to persist hybrid/content-based/collaborative recommendations and explanations.

### Not yet implemented (planned)

- **Gamification APIs**: award XP, record events, list/award badges.
- **Task and schedule APIs**: StudyTask CRUD, ScheduleSuggestion read/write.
- **List users**: `GET /api/users` (if the AI service needs to iterate all users).
- **Authentication/authorization**: endpoints are currently unauthenticated.

---

## Documents in this folder

| Document | Audience | Content |
|----------|----------|---------|
| **[README.md](./README.md)** (this file) | Everyone | Overview, stack, architecture, what’s implemented |
| **[API-Reference.md](./API-Reference.md)** | AI service, Frontend, Developers | All endpoints, request/response shapes, status codes |
| **[EDM-Layer.md](./EDM-Layer.md)** | Thesis, Developers | EDM data sources, KPIs, mastery rules, suggested difficulty, ML integration notes |
| **[For-AI-Service.md](./For-AI-Service.md)** | AI service (Python) | Which endpoints to call, recommendation write contract, examples |
| **[For-Frontend.md](./For-Frontend.md)** | Frontend | Endpoints by feature, response shapes, usage notes |

---

## Running the backend

1. **Prerequisites**: .NET 8 SDK, PostgreSQL.
2. **Configuration**: Set `ConnectionStrings:DefaultConnection` (e.g. in `appsettings.Development.json`).
3. **Run**: From the backend folder, `dotnet run --project EducationalCompanion.Api`. The API listens at **`http://localhost:5235`**. In Development, migrations run and seed data is applied; Swagger UI is available at `http://localhost:5235/swagger`.

---

## For the thesis

- Use **README.md** and **EDM-Layer.md** to describe the system and the EDM layer.
- Use **API-Reference.md** to describe the API surface and data contracts.
- Reference **For-AI-Service.md** and **For-Frontend.md** when describing integration with the AI service and the frontend.
