# Intelligent Educational Companion System

Diploma thesis implementation (Babeș-Bolyai University, 2026) — **Design of an Intelligent Educational Companion System Using Hybrid Recommendation Algorithms and Adaptive Learning Support**.

The project is a modular web application that helps learners discover suitable learning resources, track study interactions, organize tasks, and receive **personalized, level-aware recommendations** in an **access-aware** catalogue. The recommendation engine runs as a separate Python service and is evaluated offline on enriched [OULAD](https://analyse.kmi.open.ac.uk/open_dataset/) data.

**Author:** Viorelia-Maria Motofelea  
**Supervisor:** Conf. Dr. Maria-Iuliana Bocicor

---

## What the system does

| Capability | Description |
|------------|-------------|
| **Hybrid recommendations** | Combines content-based similarity (TF–IDF, optional semantic embeddings), collaborative filtering, and difficulty alignment with estimated learner level |
| **Access-aware catalogue** | Distinguishes global, course-scoped, group-scoped, and private resources; ranking and display respect visibility rules |
| **Resource ingestion** | Attach supported files (`.txt`, `.md`, `.docx`, selectable-text `.pdf`); extract text and enrich accessible catalog entries |
| **Interaction tracking** | Record views, completions, ratings, and time spent |
| **EDM analytics & mastery** | Per-learner analytics, topic mastery, and suggested difficulty for ranking |
| **Study organization** | Tasks, calendar view, and dashboard “today plan” blocks from top recommendations |
| **Offline evaluation** | Reproducible OULAD experiments (temporal split, multiple hybrid variants, beyond-accuracy metrics) |

---

## Architecture

```
┌─────────────────┐     REST (JWT)      ┌──────────────────────┐
│  React + TS     │ ◄──────────────────►│  ASP.NET Core API    │
│  (Vite)         │                     │  + EF Core           │
│  localhost:5173 │                     │  + PostgreSQL        │
└─────────────────┘                     │  localhost:5235      │
                                        └──────────┬───────────┘
                                                   │ HTTP
                                                   ▼
                                        ┌──────────────────────┐
                                        │  FastAPI AI service  │
                                        │  (hybrid recommender)│
                                        │  localhost:8001      │
                                        └──────────────────────┘
```

Monorepo layout:

| Path | Role |
|------|------|
| [`backend/`](backend/) | REST API, persistence, EDM read layer, ingestion, auth, orchestration to AI service |
| [`ai-service/`](ai-service/) | Hybrid recommendation engine and evaluation helpers |
| [`frontend/`](frontend/) | Learner-facing SPA (dashboard, recommendations, tasks, calendar, profile, ingestion) |
| [`datasets/`](datasets/) | Demo seed data and OULAD processed research artifacts |
| [`scripts/`](scripts/) | Dataset pipelines, OULAD experiments, demo tooling |

Each main folder has its own `README.md` with component-specific details.

---

## Tech stack

| Layer | Technologies |
|-------|----------------|
| Frontend | React 19, TypeScript, Vite, React Router |
| Backend | .NET 8, ASP.NET Core, Entity Framework Core, PostgreSQL, JWT (ASP.NET Identity) |
| AI service | Python, FastAPI, Uvicorn, scikit-learn, pandas, numpy; optional `sentence-transformers` for semantic mode |
| Data | PostgreSQL (runtime); JSON/SQL demo seeds; OULAD offline evaluation bundles |
| CI | GitHub Actions — pytest (AI + datasets), `dotnet test`, Vitest |

---

## Prerequisites

- **.NET 8 SDK**
- **PostgreSQL** (local instance)
- **Python 3.11+** with `pip`
- **Node.js 20+** with `npm`

Create a project virtual environment (recommended for Python scripts and the AI service):

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r ai-service/requirements.txt
pip install -r ai-service/requirements-dev.txt
```

---

## Quick start (local development)

**1. Database** — create `educational_companion_db`; set `ConnectionStrings:DefaultConnection` in `backend/EducationalCompanion.Api/appsettings.Development.json`. Development mode auto-migrates and seeds empty tables.

**2. Backend**
```bash
cd backend
dotnet run --project EducationalCompanion.Api
```

- API: **http://localhost:5235**
- Swagger (Development): **http://localhost:5235/swagger**

### 3. AI service

```bash
cd ai-service
source ../.venv/bin/activate   # if not already active
uvicorn main:app --reload --port 8001
```
Or with semantic mode enabled:
```bash
cd ai-service
source ../.venv/bin/activate   # if not already active
export SEMANTIC_CONTENT_ENABLED=true
export CONTENT_FUSION_MODE=semantic_only
export SEMANTIC_MODEL_NAME=sentence-transformers/all-MiniLM-L6-v2
uvicorn main:app --reload --port 8001
```

- API: **http://localhost:8001**
- Swagger: **http://localhost:8001/docs**
- Expects backend at `http://localhost:5235` (`config.py` or `BACKEND_BASE_URL`)

### 4. Frontend

```bash
cd frontend
cp .env.example .env    # then set VITE_API_URL
npm install
npm run dev
```

- App: **http://localhost:5173**
- Required env: `VITE_API_URL=http://localhost:5235/api`

### 5. Generate recommendations (example)

With backend and AI service running:

```bash
curl -X POST "http://localhost:8001/generate/{userId}" -H "Content-Type: application/json" -d "{}"
```

Or use the frontend: open **Recommendations** — the app can trigger generation when the stored list is empty.

---

## Frontend routes

| Route | Purpose |
|-------|---------|
| `/` | Dashboard — analytics, recommendation preview, today plan |
| `/recommendations` | Personalized recommendations with explanations |
| `/tasks` | Study tasks |
| `/calendar` | Calendar / scheduling view |
| `/profile` | Profile and learning preferences |
| `/demo/ingestion` | Supplementary materials (upload + text extraction) |
| `/login`, `/register` | Authentication |

---

## Supplementary materials

Open `/demo/ingestion` with the backend running.

| Scenario | Learner | Resource | Expected |
|----------|---------|----------|----------|
| Course upload | Alex | Week 3 Reading — Normalization | Upload succeeds; summary visible in catalog |
| Access check | Bianca | Same course resource | Resource not in catalog; upload disabled |

---

## Testing

From the repository root:

```bash
# AI service
python -m pytest ai-service/tests

# Dataset scripts
python -m pytest scripts/datasets

# Backend
dotnet test backend/EducationalCompanion.Tests/EducationalCompanion.Tests.csproj -c Release
dotnet test backend/EducationalCompanion.Tests.Integration/EducationalCompanion.Tests.Integration.csproj -c Release

# Frontend
cd frontend && npm test
```

---

## OULAD data

Download from the [OULAD open dataset](https://analyse.kmi.open.ac.uk/open_dataset/) site under their terms and place CSV files under `datasets/oulad/raw/` locally. Thesis evaluation results: `datasets/oulad/processed/eval_n5000/`. Pipeline scripts: `scripts/datasets/`.
