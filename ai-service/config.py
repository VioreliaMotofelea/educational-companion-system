import os
from pathlib import Path

BACKEND_BASE_URL = "http://localhost:5235"

BACKEND_REQUEST_TIMEOUT_S = int(os.environ.get("AI_BACKEND_TIMEOUT", "30"))
BACKEND_BULK_GET_TIMEOUT_S = int(os.environ.get("AI_BACKEND_BULK_TIMEOUT", "600"))

BACKEND_DISABLE_DATA_CACHE = os.environ.get("AI_DISABLE_BACKEND_DATA_CACHE", "").lower() in (
    "1",
    "true",
    "yes",
) 

DEFAULT_RECOMMENDATION_LIMIT = 10

# Hybrid (variant=full): 0.5 * ContentScore + 0.3 * CollaborativeScore + 0.2 * DifficultyMatch
HYBRID_CONTENT_WEIGHT = 0.5      # TF-IDF content-based similarity
HYBRID_COLLAB_WEIGHT = 0.3       # KNN cosine collaborative filtering
HYBRID_DIFFICULTY_WEIGHT = 0.2   # EDM mastery / suggested difficulty match

# Hybrid (variant=no_difficulty): renormalize original content/collab weights to sum to 1.0
HYBRID_NO_DIFF_CONTENT_WEIGHT = 0.625   # 0.5 / (0.5 + 0.3)
HYBRID_NO_DIFF_COLLAB_WEIGHT = 0.375    # 0.3 / (0.5 + 0.3)

# Diversity/novelty knobs for top-k reranking
# Hybrid popularity/novelty bonus uses Completed interaction counts
HYBRID_TOPIC_PREFERENCE_BONUS = float(os.environ.get("HYBRID_TOPIC_PREFERENCE_BONUS", "0.12"))
HYBRID_NOVELTY_BONUS = float(os.environ.get("HYBRID_NOVELTY_BONUS", "0.08"))
HYBRID_TOPIC_REPEAT_PENALTY = float(os.environ.get("HYBRID_TOPIC_REPEAT_PENALTY", "0.20"))

EVALUATION_LOG_FILE = str(Path(__file__).resolve().parent / "evaluation" / "recommendation_logs.json")

# semantic content-based path - disabled by default

def _env_bool(name: str, default: bool = False) -> bool:
    raw = os.environ.get(name)
    if raw is None:
        return default
    return raw.strip().lower() in ("1", "true", "yes")


SEMANTIC_CONTENT_ENABLED = _env_bool("SEMANTIC_CONTENT_ENABLED", False)
SEMANTIC_MODEL_NAME = os.environ.get(
    "SEMANTIC_MODEL_NAME",
    "sentence-transformers/all-MiniLM-L6-v2",
)
SEMANTIC_CACHE_DIR = Path(
    os.environ.get(
        "SEMANTIC_CACHE_DIR",
        str(Path(__file__).resolve().parent / "cache" / "semantic_embeddings"),
    )
)

_CONTENT_FUSION_RAW = os.environ.get("CONTENT_FUSION_MODE", "tfidf_only").strip().lower()
_CONTENT_FUSION_ALLOWED = frozenset({"tfidf_only", "semantic_only", "tfidf_semantic"})
CONTENT_FUSION_MODE = (
    _CONTENT_FUSION_RAW if _CONTENT_FUSION_RAW in _CONTENT_FUSION_ALLOWED else "tfidf_only"
)

CONTENT_TFIDF_SUBWEIGHT = float(os.environ.get("CONTENT_TFIDF_SUBWEIGHT", "0.5"))
CONTENT_SEMANTIC_SUBWEIGHT = float(os.environ.get("CONTENT_SEMANTIC_SUBWEIGHT", "0.5"))
