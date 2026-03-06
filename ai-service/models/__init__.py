"""AI service schema models (DTOs)."""

from models.recommendation_models import (
    BackendRecommendationsResponse,
    RecommendationBatch,
    RecommendationGenerationResponse,
    RecommendationItem,
)

__all__ = [
    "BackendRecommendationsResponse",
    "RecommendationBatch",
    "RecommendationGenerationResponse",
    "RecommendationItem",
]
