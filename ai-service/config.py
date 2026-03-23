from pathlib import Path

BACKEND_BASE_URL = "http://localhost:5235"

DEFAULT_RECOMMENDATION_LIMIT = 10

# Hybrid = 0.5 * ContentScore + 0.3 * CollaborativeScore + 0.2 * DifficultyMatch
HYBRID_CONTENT_WEIGHT = 0.5      # TF-IDF content-based similarity
HYBRID_COLLAB_WEIGHT = 0.3       # KNN cosine collaborative filtering
HYBRID_DIFFICULTY_WEIGHT = 0.2   # EDM mastery / suggested difficulty match

# File used by online evaluation to persist recommendation sessions and user events.
EVALUATION_LOG_FILE = str(Path(__file__).resolve().parent / "evaluation" / "recommendation_logs.json")
