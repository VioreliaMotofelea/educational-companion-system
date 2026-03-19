import json
from pathlib import Path
from typing import List

from config import EVALUATION_LOG_FILE
from models.evaluation_models import RecommendationLog


def _ensure_parent_exists(path: str) -> None:
    Path(path).parent.mkdir(parents=True, exist_ok=True)


def load_logs(path: str = EVALUATION_LOG_FILE) -> List[RecommendationLog]:
    file_path = Path(path)
    if not file_path.exists():
        return []

    with file_path.open("r", encoding="utf-8") as f:
        raw = json.load(f)

    return [RecommendationLog.model_validate(item) for item in raw]


def save_logs(logs: List[RecommendationLog], path: str = EVALUATION_LOG_FILE) -> None:
    _ensure_parent_exists(path)
    with Path(path).open("w", encoding="utf-8") as f:
        json.dump([log.model_dump() for log in logs], f, indent=2)


def append_recommendation_session(user_id: str, recommended_items: List[str]) -> RecommendationLog:
    logs = load_logs()
    new_log = RecommendationLog(
        user_id=user_id,
        recommended_items=recommended_items,
    )
    logs.append(new_log)
    save_logs(logs)
    return new_log


def register_click(user_id: str, item_id: str) -> bool:
    return _register_event(user_id, item_id, "clicked_items")


def register_completion(user_id: str, item_id: str) -> bool:
    return _register_event(user_id, item_id, "completed_items")


def _register_event(user_id: str, item_id: str, field: str) -> bool:
    logs = load_logs()

    # Walk from latest to oldest so events attach to latest recommendation session.
    for log in reversed(logs):
        if log.user_id != user_id:
            continue
        if item_id not in log.recommended_items:
            continue
        current_values = getattr(log, field)
        if item_id not in current_values:
            current_values.append(item_id)
            save_logs(logs)
        return True

    return False
