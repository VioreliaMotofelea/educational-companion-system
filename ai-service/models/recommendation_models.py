"""
AI service DTOs / schema models for recommendations.

Layer usage:
- RecommendationItem: recommender layer output; single recommendation (resource id, score, algorithm, explanation).
- RecommendationBatch: client layer payload to backend (list of items + replaceExisting).
- BackendRecommendationsResponse: client layer response from backend after push.
- RecommendationGenerationResponse: API layer response of POST /generate/{user_id}.
"""

from typing import List

from pydantic import BaseModel, Field


class RecommendationItem(BaseModel):
    """Single recommendation: resource id, score, algorithm name, and user-facing explanation."""

    learningResourceId: str  # UUID string from backend
    score: float = Field(ge=0.0, description="Score (normalized to [0,1] when sent to backend)")
    algorithmUsed: str = Field(..., max_length=50)
    explanation: str = Field(..., max_length=1000)


class RecommendationBatch(BaseModel):
    """Payload sent to backend POST /api/users/{id}/recommendations."""

    recommendations: List[RecommendationItem]
    replaceExisting: bool = True


class BackendRecommendationsResponse(BaseModel):
    """Response from backend after creating/replacing recommendations."""

    userId: str
    createdCount: int
    replacedExisting: bool

    model_config = {
        "json_schema_extra": {
            "examples": [
                {
                    "userId": "user-1",
                    "createdCount": 10,
                    "replacedExisting": True,
                }
            ]
        }
    }


class RecommendationGenerationResponse(BaseModel):
    """Response of AI service POST /generate/{user_id}: counts and backend confirmation."""

    userId: str
    generated: int = Field(..., description="Number of recommendations generated")
    backendResponse: BackendRecommendationsResponse

    model_config = {
        "json_schema_extra": {
            "examples": [
                {
                    "userId": "user-1",
                    "generated": 10,
                    "backendResponse": {
                        "userId": "user-1",
                        "createdCount": 10,
                        "replacedExisting": True,
                    },
                }
            ]
        }
    }
