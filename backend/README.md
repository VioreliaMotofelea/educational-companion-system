# Backend — Educational Companion API

ASP.NET Core REST API for the Intelligent Educational Companion System. It is the **central persistence and orchestration layer**: user accounts, learning resources, interactions, recommendations (read/write), EDM analytics, study tasks, and resource ingestion.

Hybrid scoring runs in `ai-service/`; this layer orchestrates HTTP calls to it.

---

## Solution structure

| Project | Purpose |
|---------|---------|
| **EducationalCompanion.Domain** | Entities, enums, domain exceptions |
| **EducationalCompanion.Infrastructure** | EF Core + PostgreSQL, repositories, migrations, seed data, EDM SQL reads |
| **EducationalCompanion.Api** | Controllers, services, DTOs, middleware, JWT auth |
| **EducationalCompanion.Tests** | Unit tests |
| **EducationalCompanion.Tests.Integration** | API integration tests (SQLite in-memory) |

Architecture: **Clean Architecture** — API depends on Infrastructure; Domain has no outward dependencies.

---

## Responsibilities

- **Authentication** — register/login, JWT bearer tokens (ASP.NET Identity)
- **Learning resources** — CRUD, search, **access-filtered** catalog per user
- **User interactions** — viewed, completed, rated, skipped
- **Recommendations** — store batches from the AI service; serve ranked lists with explanations; trigger generation via HTTP to AI service
- **EDM layer** — analytics summary, per-topic mastery, suggested difficulty
- **Study tasks** — CRUD linked to users (optional resource association)
- **Resource ingestion V1** — upload supplementary files, local text extraction, capped summary on accessible catalog only

The backend **does not** compute hybrid scores; that logic lives in `ai-service/`.

---

## Prerequisites

- .NET 8 SDK
- PostgreSQL 14+ (local)

---

## Configuration

`EducationalCompanion.Api/appsettings.Development.json`:

- `ConnectionStrings:DefaultConnection` — PostgreSQL
- `Cors:AllowedOrigins` — include `http://localhost:5173`
- `AiService:BaseUrl` — default `http://localhost:8001` in `appsettings.json`
- `Jwt` — signing key and token settings

## Run

```bash
cd backend
dotnet run --project EducationalCompanion.Api
```

http://localhost:5235 · Swagger at `/swagger` in Development.

Migrations and `DatabaseSeeder` run on startup when the database is empty.

## Main endpoints

| Area | Path |
|------|------|
| Auth | `/api/auth/register`, `/api/auth/login` |
| Profile, preferences, analytics, mastery, tasks | `/api/users/{id}/...` |
| Recommendations | `GET/POST /api/users/{id}/recommendations`, `POST .../recommendations/generate` |
| Full catalog | `/api/resources` |
| Accessible catalog (ranking input) | `/api/users/{id}/resources/accessible` |
| Interactions | `/api/interactions` |
| File ingestion | `POST /api/resources/{resourceId}/files` |

Recommendations are written by the AI service and filtered by the same access rules as the accessible catalogue.

## Ingestion V1

Upload supplementary files to an existing resource. Supported: `.txt`, `.md`, `.markdown`, `.docx`, selectable-text `.pdf`. Text is extracted locally; a capped summary appears on **accessible** catalog rows only. OCR and scanned PDFs are not supported.

## Test

```bash
dotnet test EducationalCompanion.Tests/EducationalCompanion.Tests.csproj -c Release
dotnet test EducationalCompanion.Tests.Integration/EducationalCompanion.Tests.Integration.csproj -c Release
```
