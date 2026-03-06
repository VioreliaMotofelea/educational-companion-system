# Hybrid Recommendation Algorithm for the Educational Companion System

This document provides an academic-style description of the recommendation engine implemented in the AI service. It is intended for **thesis writing** and **technical reference**.

---

## Overview

The Educational Companion System integrates an intelligent recommendation module designed to support personalized learning. The recommendation engine is implemented as a **hybrid recommender system** that combines:

1. **Content-based filtering** — similarity between learning resources based on textual features  
2. **Collaborative filtering** — patterns among users with similar learning behaviors  
3. **Learning progression adaptation** — derived from Educational Data Mining (EDM) on the backend  

The goal is to generate personalized recommendations for learning resources by considering both the characteristics of educational content and the behavioral patterns of learners.

The recommendation engine is implemented as a **separate AI microservice** developed in Python using the FastAPI framework. This service communicates with the backend system through REST APIs. The backend provides access to user profiles, interaction history, and the catalog of learning resources. After computing recommendations, the AI service sends the resulting recommendations back to the backend via a dedicated API endpoint, where they are stored and later served to the frontend application.

The hybrid recommender operates in **three stages**: content-based filtering, collaborative filtering, and weighted hybrid ranking.

---

## Content-Based Filtering

Content-based filtering recommends resources that are **similar to those previously consumed by the user**. This method relies on analyzing the textual characteristics of learning resources and comparing them with the user’s interaction history.

### Representation

Each learning resource is represented using a **textual description** constructed from several attributes, including the **title**, **description**, **topic**, and content type. These textual features are transformed into numerical vectors using the **Term Frequency–Inverse Document Frequency (TF-IDF)** representation.

### TF-IDF

Formally, for each resource *r*, a feature vector is generated using TF-IDF:

**TF-IDF(t, d) = TF(t, d) × log( N / df(t) )**

where:

- *t* represents a term  
- *d* represents a document (resource description)  
- *N* is the number of resources  
- *df(t)* is the number of documents containing term *t*  

### Similarity

The similarity between resources is computed using **cosine similarity**:

**sim(A, B) = (A · B) / (‖A‖ × ‖B‖)**

where *A* and *B* are TF-IDF vectors representing two resources (· is the dot product, ‖·‖ is the vector norm).

### Recommendation logic

The system identifies resources that the user has **previously completed** (or rated positively), then recommends new resources whose TF-IDF vectors are most similar to those previously consumed. This produces the **ContentScore**, which represents how relevant a resource is based on its similarity to the user’s past learning activities.

**Implementation reference:** `recommender/content_based.py` — TF-IDF vectorization (e.g. scikit-learn `TfidfVectorizer`), cosine similarity over the resource corpus, averaging over completed resources, ranking by similarity.

---

## Collaborative Filtering

Collaborative filtering complements content-based filtering by identifying **patterns among users with similar learning behaviors**. Instead of analyzing resource content, this method analyzes interactions between users and resources.

### User–item matrix

User interactions with resources are represented in a **user–item interaction matrix**, where rows represent users and columns represent learning resources. Each matrix entry indicates whether a user interacted with a particular resource. In the current implementation, interactions such as **resource completion** are encoded as binary values.

Example interaction matrix:

| User   | Resource A | Resource B | Resource C |
|--------|-------------|------------|-------------|
| User 1 | 1           | 1          | 0           |
| User 2 | 1           | 1          | 1           |
| User 3 | 0           | 1          | 1           |

### User similarity

To identify similar learners, **cosine similarity** is used again, this time between **user interaction vectors**. For two users *u*ᵢ and *u*ⱼ, similarity is computed as:

**sim(uᵢ, uⱼ) = (uᵢ · uⱼ) / (‖uᵢ‖ × ‖uⱼ‖)**

### Nearest neighbors and scoring

The system then selects the **K nearest neighbors** (most similar users). Resources that were interacted with by these similar users but **not yet consumed by the target user** become candidate recommendations. Recommendation scores are computed by aggregating the weighted contributions of similar users (e.g. weighted sum of neighbor vectors).

This process produces the **CollaborativeScore**, which reflects how likely a resource is to be relevant based on the behavior of similar learners.

**Implementation reference:** `recommender/collaborative.py` — user–resource pivot matrix, cosine similarity between users, KNN selection, weighted scores per resource, exclusion of already-completed items.

---

## Learning Progression Adaptation Using EDM

In addition to traditional recommendation techniques, the system integrates **Educational Data Mining (EDM)** insights. The backend EDM layer analyzes user interactions to estimate learning mastery levels and recommend an appropriate difficulty level for future resources.

### Mastery API

The AI service retrieves this information via the backend endpoint:

```
GET /api/users/{userId}/mastery
```

The mastery response provides:

- **Mastery level per topic** (e.g. None, Beginner, Intermediate, Advanced)  
- **Suggested difficulty level** (1–5) for future learning resources  

### Use in recommendations

This information is used to **filter or prioritize** candidate resources whose difficulty level matches the learner’s current knowledge level. In the implementation, a **DifficultyMatch** score is computed for each candidate resource: it is 1 when the resource’s difficulty equals the suggested difficulty, and decreases as the gap increases (e.g. **1 − gap/4** for a 1–5 scale). This component ensures that recommendations support **pedagogically appropriate progression**, preventing learners from receiving content that is either too difficult or too trivial.

This mechanism produces the **DifficultyMatch** score (in **[0, 1]**).

**Implementation reference:** `recommender/hybrid.py` — reads `suggestedDifficulty` from mastery (or falls back to user preferences); for each candidate resource, computes difficulty gap and normalizes to a 0–1 match score.

---

## Hybrid Recommendation Model

To produce the **final recommendation ranking**, the system combines the outputs of the three components using a **weighted hybrid model**.

### Formula

The final recommendation score is computed as:

**FinalScore = w₁ · ContentScore + w₂ · CollaborativeScore + w₃ · DifficultyMatch**

where *w*₁, *w*₂, and *w*₃ are weighting coefficients.

### Weights (current implementation)

In the current implementation, the following weights are used:

- **w₁ = 0.5** — ContentScore (TF-IDF content-based similarity)  
- **w₂ = 0.3** — CollaborativeScore (KNN collaborative filtering)  
- **w₃ = 0.2** — DifficultyMatch (EDM mastery alignment)  

These weights **prioritize content similarity** while still considering collaborative signals and learning progression constraints.

**Implementation reference:** `config.py` — `HYBRID_CONTENT_WEIGHT`, `HYBRID_COLLAB_WEIGHT`, `HYBRID_DIFFICULTY_WEIGHT`; `recommender/hybrid.py` — normalization of content and collaborative scores to [0, 1], computation of DifficultyMatch, weighted sum, ranking, and top-N selection.

### Ranking and delivery

Candidate resources are **ranked** according to their FinalScore, and the system selects the **top N** items as final recommendations (e.g. *N* = 10 by default). These are sent to the backend with score, algorithm label, and explanation.

---

## Explainable Recommendations

Each recommended resource is accompanied by a **human-readable explanation** that describes the reason for the recommendation. Examples include:

- *“Similar to resources you completed in Python.”*  
- *“Recommended because similar users completed this resource.”*  
- *“Content match 0.85, similar users 0.72, difficulty fit 1.00 (suggested level 3).”*  

Providing explanations improves **transparency** and increases **user trust** in the recommendation system.

**Implementation reference:** Each recommender module and the hybrid layer set an `explanation` field on every recommendation item; the backend stores and returns it to the frontend.

---

## Summary

The proposed recommendation system combines **three complementary techniques**:

1. **Content-based filtering** — to capture semantic similarity between learning resources (TF-IDF + cosine similarity).  
2. **Collaborative filtering** — to exploit patterns across learners with similar behavior (user–item matrix, KNN, cosine similarity).  
3. **EDM-driven difficulty adaptation** — to align recommendations with the learner’s knowledge level (suggested difficulty and DifficultyMatch score).  

The **hybrid architecture** improves recommendation robustness by mitigating the limitations of individual methods:

- **Content-based filtering** addresses cold-start situations for users with limited history.  
- **Collaborative filtering** captures collective learning patterns.  
- The integration of **EDM analytics** ensures that recommendations support effective learning progression.  

This hybrid recommender system forms the **core intelligent component** of the Educational Companion System, enabling personalized, explainable, and pedagogically relevant recommendations for learners.
