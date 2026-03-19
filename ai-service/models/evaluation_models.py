"""DTOs for recommendation evaluation logging and reporting."""

from typing import List

from pydantic import BaseModel, Field


class RecommendationLog(BaseModel):
    user_id: str
    recommended_items: List[str]
    clicked_items: List[str] = Field(default_factory=list)
    completed_items: List[str] = Field(default_factory=list)


class InteractionEventRequest(BaseModel):
    user_id: str
    item_id: str


class InteractionEventResponse(BaseModel):
    message: str
    user_id: str
    item_id: str


class EvaluationReportResponse(BaseModel):
    k: int
    logs_count: int
    precision_at_k: float
    recall_at_k: float
    ndcg_at_k: float
    ctr: float
    completion_rate: float
