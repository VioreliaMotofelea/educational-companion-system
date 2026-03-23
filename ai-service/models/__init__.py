"""AI service schema models (DTOs)."""

from models.recommendation_models import (
    BackendRecommendationsResponse,
    RecommendationBatch,
    RecommendationGenerationResponse,
    RecommendationItem,
)
from models.evaluation_models import (
    EvaluationReportResponse,
    InteractionEventRequest,
    InteractionEventResponse,
    RecommendationLog,
)

__all__ = [
    "BackendRecommendationsResponse",
    "RecommendationBatch",
    "RecommendationGenerationResponse",
    "RecommendationItem",
    "EvaluationReportResponse",
    "InteractionEventRequest",
    "InteractionEventResponse",
    "RecommendationLog",
]
