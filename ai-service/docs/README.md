# Educational Companion — AI Service Documentation

This folder contains documentation for the **Educational Companion** AI service: the recommendation microservice that generates personalized learning resource recommendations. It supports **thesis writing**, **further development**, and integration with the **backend** and **frontend**.

---

## Purpose of the AI service

The AI service is a separate microservice that implements the **hybrid recommendation engine** for the Educational Companion system. It:

- **Reads** from the backend: user profiles, user interactions, all users’ interactions (for collaborative filtering), the learning resource catalog, and EDM mastery (suggested difficulty).
- **Computes** hybrid recommendations by combining:
  1. **Content-based filtering** (TF-IDF + cosine similarity)
  2. **Collaborative filtering** (user–item matrix, KNN cosine similarity)
  3. **EDM difficulty adaptation** (match between resource difficulty and suggested level)
- **Writes** the resulting recommendations back to the backend via `POST /api/users/{id}/recommendations`, where they are stored and later served to the frontend.

The backend does not generate recommendations; it only stores and serves them. The AI service is the single place where recommendation logic runs.

---

## Tech stack

| Layer | Technology                                              |
|-------|---------------------------------------------------------|
| Runtime | Python 3.14.0                                           |
| API | FastAPI                                                 |
| Server | Uvicorn (ASGI)                                          |
| ML / similarity | scikit-learn (TF-IDF, cosine similarity), pandas, numpy |
| HTTP client | requests                                                |

---

## Architecture

The AI service is organized into clear layers (each directory is a Python package with `__init__.py`):

| Layer | Directory | Role |
|-------|------------|------|
| **API** | `api/` | Routes, request/response handling, exception handlers (BackendError, ValidationError, Exception). |
| **Clients** | `clients/` | HTTP calls to the backend (user, interactions, resources, mastery, push recommendations). All errors converted to `BackendError`. |
| **Models** | `models/` | Pydantic DTOs: `RecommendationItem`, `RecommendationBatch`, `BackendRecommendationsResponse`, `RecommendationGenerationResponse`. |
| **Recommender** | `recommender/` | Pure recommendation logic: `content_based.py` (TF-IDF), `collaborative.py` (KNN cosine), `hybrid.py` (weighted combination + EDM difficulty). |
| **Config** | `config.py` | Central configuration: backend URL, hybrid weights (0.5, 0.3, 0.2), default recommendation limit. |

- **Flow:** A request hits the API → routes orchestrate (fetch data via client, call recommender, push via client) → response returned. Business logic lives in the recommender; I/O and errors are handled in the client and API.

---

## What is implemented (current state)

- **Single endpoint:** `POST /generate/{user_id}` — generates hybrid recommendations for the given user and persists them to the backend.
- **Content-based:** TF-IDF over title + topic + description; cosine similarity to user’s completed resources; outputs `ContentScore` per candidate.
- **Collaborative:** User–resource matrix (completed = 1); user–user cosine similarity; K nearest neighbors; weighted scores; outputs `CollaborativeScore` per candidate.
- **EDM difficulty:** Reads `GET /api/users/{id}/mastery` for `suggestedDifficulty`; computes `DifficultyMatch` as alignment between resource difficulty and suggested level (0–1).
- **Hybrid:** `FinalScore = 0.5·ContentScore + 0.3·CollaborativeScore + 0.2·DifficultyMatch`; candidates ranked by `FinalScore`; top N returned and pushed to backend with explanations.
- **Error handling:** `BackendError` (backend failures), `ValidationError` (invalid data), and generic `Exception` handled globally; consistent JSON error responses and logging.
- **Logging:** Request start/success and backend/validation/unhandled errors logged with a consistent format.

---

## Documents in this folder

| Document | Audience | Content |
|----------|----------|---------|
| **[README.md](./README.md)** (this file) | Everyone | Overview, stack, architecture, what’s implemented |
| **[Hybrid-Recommendation-Algorithm.md](./Hybrid-Recommendation-Algorithm.md)** | Thesis, Developers | Academic-style description of the hybrid algorithm (content-based, collaborative, EDM, formula, explainability) |
| **[API-Reference.md](./API-Reference.md)** | Backend, Frontend, Developers | AI service endpoint, request/response, error codes |
| **[Evaluation-Module.md](./Evaluation-Module.md)** | Thesis, Developers | Evaluation module design, metrics, integration flow, and end-to-end testing guide |

---

## Running the AI service

1. **Prerequisites:** Python 3.x, pip. Backend must be running (e.g. at `http://localhost:5235`).
2. **Install:** From the `ai-service` folder, `pip install -r requirements.txt`.
3. **Configuration:** Set `BACKEND_BASE_URL` in `config.py` if the backend is not at `http://localhost:5235`.
4. **Run:** From the `ai-service` folder, `uvicorn main:app --reload --port 8001`. The API listens at **`http://localhost:8001`**. Swagger UI: `http://localhost:8001/docs`.

---

## For the thesis

- Use **README.md** for the high-level role of the AI service and its architecture.
- Use **Hybrid-Recommendation-Algorithm.md** for the formal description of the recommendation algorithm (content-based, collaborative, EDM, hybrid formula, explainability).
- Use **API-Reference.md** when describing how the backend or frontend triggers and consumes recommendations.
- Use **Evaluation-Module.md** when describing how recommendation quality is measured and how metrics are validated.
