# Frontend — Educational Companion Web App

React 19 + TypeScript single-page application (Vite) for the Intelligent Educational Companion System. It provides the learner interface: dashboard, personalized recommendations, study tasks, calendar, profile, and a route for resource ingestion.

---

## Features (UI)

| Area | Route | Description |
|------|-------|-------------|
| Dashboard | `/` | Learning analytics, recommendation preview, suggested focus blocks |
| Recommendations | `/recommendations` | Ranked resources with hybrid explanations; can trigger AI generation |
| Tasks | `/tasks` | Study task list and management |
| Calendar | `/calendar` | Schedule-oriented view of tasks and study time |
| Profile | `/profile` | Account details, preferences, mastery / difficulty insights |
| Supplementary materials | `/materials` | Upload files with automatic text extraction |
| Auth | `/login`, `/register` | JWT-based sign-in |

All main routes except login/register are protected (`ProtectedRoute`).

---

## Project structure

```
src/
├── components/     # Layout, dashboard, recommendations, tasks, calendar, …
├── pages/          # Route-level screens
├── routes/         # React Router setup
├── services/       # API clients (auth, users, recommendations, ingestion, …)
├── hooks/          # Data-fetching and UI hooks
├── context/        # Auth context
├── utils/          # Formatting, recommendation explanation parsing
├── constants/      # Shared constants (e.g. calendar timezone)
└── styles/         # Global CSS
```

---

## Prerequisites

- Node.js 20+
- Backend API running at `http://localhost:5235`
- AI service running at `http://localhost:8001` (for on-demand recommendation generation)

---

## Configuration

```bash
cp .env.example .env
```

Required:

```env
VITE_API_URL=http://localhost:5235/api
```

Optional:

```env
# Calendar timezone; default Europe/Bucharest when unset
# VITE_CALENDAR_TIMEZONE=local
```

---

## Run (development)

```bash
cd frontend
npm install
npm run dev
```

App: **http://localhost:5173**

The backend must allow this origin in `Cors:AllowedOrigins` (default in Development).

---

## Build & preview

```bash
npm run build
npm run preview
```

Production build output: `dist/`

---

## Test

```bash
npm test          # Vitest (unit tests under src/**/*.test.ts)
npm run lint      # ESLint
```

---

## API integration

The frontend talks **only to the backend** (`VITE_API_URL`). It does not call the AI service directly.

Typical flows:

1. **Login** → `POST /api/auth/login` → JWT stored in browser storage
2. **Recommendations** → `GET /api/users/{id}/recommendations`; if empty, `POST /api/users/{id}/recommendations/generate`
3. **Interactions** → `POST /api/interactions` when starting/completing/rating a resource
4. **Ingestion** → multipart upload to `/api/resources/{id}/files` for the signed-in user

---

## Supplementary materials

Route `/materials` — attach files to learning resources in **your** catalog with automatic text extraction.

- Register `alex.demo@example.com` and `bianca.demo@example.com`, then run `python3 scripts/datasets/seed_demo_catalog.py --apply-sql`.
- **Alex** sees *Week 3 Reading — Normalization* (Course only); upload `databases-normalization-notes.md` → summary appears.
- **Bianca** does not see that course reading in the dropdown (access control demo).

Supported: `.txt`, `.md`, `.markdown`, `.docx`, selectable-text `.pdf`.
