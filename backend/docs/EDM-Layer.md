# Educational Data Mining (EDM) Layer — Documentation

This document describes the **Educational Data Mining layer** of the Educational Companion backend. It is intended to support the thesis and future development (e.g. ML integration).

---

## 1. Overview

The EDM layer exposes **analytics**, **recommendations**, and **mastery** for each user to support an AI + Data Mining–enhanced adaptive educational system. It is implemented as a dedicated read-oriented layer that aggregates data from existing domain entities without duplicating business logic.

**API endpoints:**

| Endpoint | Purpose |
|----------|--------|
| `GET /api/users/{id}/analytics` | User analytics summary and KPIs (for dashboards and reporting) |
| `GET /api/users/{id}/recommendations` | Personalized content list (with score, algorithm, explanation); optional `?limit=N` |
| `GET /api/users/{id}/mastery` | Topic-level mastery and suggested difficulty for adaptive learning |

---

## 2. Data Sources

All EDM outputs are derived from existing persistence. No separate EDM database or warehouse is used.

### 2.1 Tables / entities used

| Source | Use in EDM |
|--------|------------|
| **UserProfiles** | User existence check; Level, XP (total earned) for analytics KPIs |
| **UserInteractions** | Viewed/Completed counts, completion rate, average rating, total time spent; completed resources per topic for mastery |
| **LearningResources** | Topic, Difficulty, metadata; joined with interactions (mastery) and with Recommendations (content list) |
| **Recommendations** | Precomputed recommendations per user (LearningResourceId, Score, AlgorithmUsed, Explanation); served as content list |
| **StudyTasks** | Task counts by status (Completed, Pending, Overdue) for analytics KPIs |
| **GamificationEvents** | Count of gamification events per user for analytics KPIs |

### 2.2 Interaction types (domain)

- **Viewed** — resource was opened/viewed  
- **Completed** — user completed the resource  
- **Rated** — user gave a 1–5 rating (optional on other types)  
- **Skipped** — user skipped the resource  

Only **Viewed** and **Completed** are used for EDM counts and completion rate. **Rated** and **Rating** are used for average rating and mastery quality.

---

## 3. Analytics — KPIs and Summary

**Implementation:** `IUserEdmReadRepository.GetUserAnalyticsKpisAsync` (Infrastructure) → `IUserEdmService.GetAnalyticsAsync` (Api) → `UserAnalyticsResponse`.

### 3.1 KPIs defined

| KPI | Definition | Data source |
|-----|-------------|-------------|
| **TotalResourcesViewed** | Count of interactions with `InteractionType = Viewed` for the user | UserInteractions |
| **TotalResourcesCompleted** | Count of interactions with `InteractionType = Completed` for the user | UserInteractions |
| **CompletionRatePercent** | `(TotalResourcesCompleted / TotalResourcesViewed) × 100`; 0 if no views | Derived |
| **AverageRatingGiven** | Mean of `Rating` over all interactions that have a non-null rating (1–5) | UserInteractions |
| **TotalTimeSpentMinutes** | Sum of `TimeSpentMinutes` over all interactions | UserInteractions |
| **TotalXpEarned** | Current total XP of the user | UserProfiles.Xp |
| **CurrentLevel** | Current level of the user | UserProfiles.Level |
| **TasksCompleted** | Count of StudyTasks with `Status = Completed` | StudyTasks |
| **TasksPending** | Count of StudyTasks with `Status = Pending` | StudyTasks |
| **TasksOverdue** | Count of StudyTasks with `Status = Overdue` | StudyTasks |
| **GamificationEventsCount** | Count of GamificationEvents for the user | GamificationEvents |

### 3.2 Summary text

The **Summary** is a short narrative built from the KPIs (e.g. “X of Y viewed resources completed (Z% completion rate). Total study time: N minutes. Level L, XP; M gamification events. Tasks: …”). It is generated in `UserEdmService.BuildAnalyticsSummary` and returned as `UserAnalyticsResponse.Summary.SummaryText` with `ComputedAtUtc`.

---

## 4. Mastery Rules and Suggested Difficulty

**Implementation:** `IUserEdmReadRepository.GetTopicMasteryDataAsync` (per-topic aggregates) → `UserEdmService.GetMasteryAsync` (mastery level + suggested difficulty) → `UserMasteryResponse`.

### 4.1 Topic mastery data (per topic)

For each **topic** (from LearningResource.Topic), we consider only **completed** resources (InteractionType = Completed) and compute:

- **ResourcesCompleted** — number of completed resources in that topic  
- **AverageRating** — mean of user ratings for those completions (null if no ratings)  
- **AverageDifficultyCompleted** — mean of LearningResource.Difficulty (1–5) for those resources  

These are computed in `UserEdmReadRepository.GetTopicMasteryDataAsync` (grouping completed interactions by `LearningResource.Topic`).

### 4.2 Mastery level (per topic)

The **MasteryLevel** label is derived in `UserEdmService.DeriveMasteryLevel`:

| Condition | MasteryLevel |
|-----------|--------------|
| No resources completed in topic | **None** |
| ResourcesCompleted ≥ 5 and AverageRating ≥ 4.0 | **Advanced** |
| ResourcesCompleted ≥ 3 | **Intermediate** |
| Otherwise | **Beginner** |

So: “Advanced” requires both volume (≥5) and quality (rating ≥ 4.0); “Intermediate” only volume (≥3); “Beginner” is 1–2 completions or low rating.

### 4.3 Suggested difficulty (global, 1–5)

A single **SuggestedDifficulty** (1–5) is computed in `UserEdmService.DeriveSuggestedDifficulty`:

1. **No completed topics:** return difficulty **1** with reason “No completed topics yet; start with difficulty 1.”
2. **Otherwise:**
   - **avgDifficulty** = mean of `AverageDifficultyCompleted` over all topics the user has completed.
   - **avgRating** = mean of per-topic `AverageRating` (topics with no rating contribute 0).
   - **Formula:**
     - If **avgRating ≥ 4.0:**  
       `suggested = min(5, ceil(avgDifficulty) + 1)`  
       (suggest next level up, cap at 5).
     - Else:  
       `suggested = round(avgDifficulty)`  
       (stay at current average level).
   - **Clamp** the result to **[1, 5]**.
   - **Reason** string: “Based on N topic(s); average completed difficulty X, rating Y. Suggested next: Z.”

This gives a simple rule-based “next difficulty” for adaptive content (e.g. when filtering or ranking resources by difficulty).

---

## 5. Recommendations (Content List)

**Implementation:** `IRecommendationRepository.GetByUserIdWithResourceAsync` → `IUserEdmService.GetRecommendationsAsync` → list of `UserRecommendationItemResponse`.

- Recommendations are **stored** in the **Recommendations** table (UserId, LearningResourceId, Score, AlgorithmUsed, Explanation, CreatedAtUtc).
- The EDM layer **does not generate** recommendations; it **reads** and **serves** them with resource details (title, topic, difficulty, duration, content type, etc.).
- Results are ordered by **Score** (desc), then **CreatedAtUtc** (desc). Optional query `?limit=N` caps the number returned.

So: **recommendation generation** (content-based, collaborative, hybrid, or future ML) is a separate process that **writes** into `Recommendations`; the EDM layer only **exposes** that content list to the client.

---

## 6. Where ML Would Plug In

### 6.1 Recommendation generation (write side)

- **Current:** Recommendations are created elsewhere (e.g. seed data or a separate job) and stored in `Recommendations`.
- **ML plug-in:** A recommendation **generation** service (e.g. matrix factorization, neural recommender, or LLM-based ranking) would:
  - Read: UserInteractions, UserPreferences, LearningResources, ResourceMetadata (e.g. embeddings), optionally other EDM aggregates.
  - Compute: scores and optionally explanations.
  - **Write:** new/updated rows in **Recommendations** (UserId, LearningResourceId, Score, AlgorithmUsed, Explanation).
- The **EDM read layer** stays unchanged: it continues to serve `GET /api/users/{id}/recommendations` from the same table.

### 6.2 EDM read layer (current and future)

- **Current:** EDM is **read-only**: analytics KPIs, summary, mastery rules, and suggested difficulty are computed with **deterministic rules** (counts, averages, thresholds) in `UserEdmReadRepository` and `UserEdmService`.
- **Future ML options (still “read” side):**
  - **Predictive KPIs:** e.g. dropout risk or “expected completion rate” from a small model that uses the same EDM data sources; could be added as extra fields in the analytics response.
  - **Mastery / difficulty:** replace or augment the rule-based “MasteryLevel” and “SuggestedDifficulty” with model outputs (e.g. Bayesian knowledge tracing, IRT, or a small classifier). The API contract (`UserMasteryResponse`) can stay the same; only the implementation of `GetMasteryAsync` (and possibly a new EDM read component that calls the model) would change.
  - **Summary text:** NLG or an LLM could generate the analytics summary from KPIs instead of the current template; again, same endpoint, different implementation behind `GetAnalyticsAsync`.

So:

- **Recommendation generation** = **write path** → writes into `Recommendations`; EDM only reads.
- **EDM read layer** = **read path** → analytics, mastery, suggested difficulty; can later call ML models for richer KPIs, mastery, or summary without changing the API.

---

## 7. Code Locations (Current Implementation)

| Concern | Layer | Type / File |
|--------|--------|-------------|
| EDM API contract | Api | `IUserEdmService` |
| Analytics, recommendations, mastery orchestration | Api | `UserEdmService` |
| Analytics & mastery DTOs | Api | `Dtos/Analytics/`, `Dtos/Mastery/`, `Dtos/Recommendations/` |
| Analytics KPIs & topic mastery queries | Infrastructure | `IUserEdmReadRepository`, `UserEdmReadRepository` (Edm/) |
| Recommendation list query | Infrastructure | `IRecommendationRepository`, `RecommendationRepository` |
| EDM data transfer (KPI/topic raw data) | Infrastructure | `UserAnalyticsKpisData`, `TopicMasteryData` (Edm/) |
| HTTP endpoints | Api | `UsersController`: `.../analytics`, `.../recommendations`, `.../mastery` |

---

## 8. Summary for Thesis

- **Data sources:** UserProfiles, UserInteractions, LearningResources, Recommendations, StudyTasks, GamificationEvents (all existing tables).
- **KPIs:** Defined in Section 3; all are derived from counts, sums, and averages over these sources.
- **Mastery:** Per-topic completion counts and average rating/difficulty; rule-based labels (None / Beginner / Intermediate / Advanced) and a global suggested difficulty (1–5) with a clear formula (Section 4).
- **Recommendations:** Served from stored `Recommendations`; generation (including future ML) is a separate write-side component; EDM only reads and exposes the content list.
- **ML integration:** Recommendation generation = write into `Recommendations`. Enrichment of analytics/mastery/summary = optional ML behind the same EDM read layer and endpoints.

This document reflects the implementation as of the current codebase and can be extended as new KPIs, mastery rules, or ML components are added.
