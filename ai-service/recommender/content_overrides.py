"""Optional runtime overrides for content-based fusion (evaluation / ablations)."""

from __future__ import annotations

from dataclasses import dataclass

from config import (
    CONTENT_FUSION_MODE,
    CONTENT_SEMANTIC_SUBWEIGHT,
    CONTENT_TFIDF_SUBWEIGHT,
    SEMANTIC_CONTENT_ENABLED,
    SEMANTIC_MODEL_NAME,
)

_ALLOWED_FUSION_MODES = frozenset({"tfidf_only", "semantic_only", "tfidf_semantic"})


@dataclass(frozen=True)
class ContentModeOverrides:
    semantic_enabled: bool | None = None
    content_fusion_mode: str | None = None
    content_tfidf_subweight: float | None = None
    content_semantic_subweight: float | None = None
    semantic_model_name: str | None = None

    def resolved_semantic_enabled(self) -> bool:
        if self.semantic_enabled is not None:
            return self.semantic_enabled
        return SEMANTIC_CONTENT_ENABLED

    def resolved_fusion_mode(self) -> str:
        if not self.resolved_semantic_enabled():
            return "tfidf_only"
        raw = self.content_fusion_mode if self.content_fusion_mode is not None else CONTENT_FUSION_MODE
        normalized = str(raw).strip().lower()
        if normalized not in _ALLOWED_FUSION_MODES:
            return "tfidf_only"
        return normalized

    def resolved_tfidf_subweight(self) -> float:
        if self.content_tfidf_subweight is not None:
            return float(self.content_tfidf_subweight)
        return CONTENT_TFIDF_SUBWEIGHT

    def resolved_semantic_subweight(self) -> float:
        if self.content_semantic_subweight is not None:
            return float(self.content_semantic_subweight)
        return CONTENT_SEMANTIC_SUBWEIGHT

    def resolved_semantic_model_name(self) -> str:
        if self.semantic_model_name is not None:
            return self.semantic_model_name
        return SEMANTIC_MODEL_NAME

    def as_dict(self) -> dict:
        return {
            "semantic_enabled": self.resolved_semantic_enabled(),
            "content_fusion_mode": self.resolved_fusion_mode(),
            "content_tfidf_subweight": self.resolved_tfidf_subweight(),
            "content_semantic_subweight": self.resolved_semantic_subweight(),
            "semantic_model_name": self.resolved_semantic_model_name(),
        }


def overrides_for_fusion_mode(mode: str, *, semantic_model_name: str | None = None) -> ContentModeOverrides:
    """Build overrides for a named fusion mode (``tfidf_only``, ``semantic_only``, ``tfidf_semantic``)."""
    normalized = mode.strip().lower()
    if normalized == "tfidf_only":
        return ContentModeOverrides(
            semantic_enabled=False,
            content_fusion_mode="tfidf_only",
            semantic_model_name=semantic_model_name,
        )
    if normalized == "semantic_only":
        return ContentModeOverrides(
            semantic_enabled=True,
            content_fusion_mode="semantic_only",
            semantic_model_name=semantic_model_name,
        )
    if normalized == "tfidf_semantic":
        return ContentModeOverrides(
            semantic_enabled=True,
            content_fusion_mode="tfidf_semantic",
            semantic_model_name=semantic_model_name,
        )
    raise ValueError(f"Unsupported fusion mode: {mode!r}")
