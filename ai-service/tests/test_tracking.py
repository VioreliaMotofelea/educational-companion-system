import json
from pathlib import Path

import pytest

from evaluation import tracking


@pytest.fixture()
def isolated_default_log_file():
    """
    Tracking uses a file-based JSON log with a module-level default path.
    To keep tests deterministic and avoid cross-test contamination, we snapshot
    the current file content and restore it at the end.
    """
    path = Path(tracking.EVALUATION_LOG_FILE)
    original_exists = path.exists()
    original_text = path.read_text(encoding="utf-8") if original_exists else None

    # Start clean
    tracking.save_logs([], path=tracking.EVALUATION_LOG_FILE)

    yield path

    # Restore
    if original_exists:
        path.write_text(original_text, encoding="utf-8")
    else:
        try:
            path.unlink()
        except FileNotFoundError:
            pass


def test_append_and_register_click_completion(isolated_default_log_file):
    log = tracking.append_recommendation_session("user-1", ["a", "b"])
    assert log.user_id == "user-1"
    assert log.recommended_items == ["a", "b"]
    assert log.clicked_items == []
    assert log.completed_items == []

    assert tracking.register_click("user-1", "a") is True
    assert tracking.register_completion("user-1", "a") is True

    logs = tracking.load_logs()
    assert len(logs) == 1
    assert logs[0].clicked_items == ["a"]
    assert logs[0].completed_items == ["a"]


def test_events_attach_to_latest_session_with_item(isolated_default_log_file):
    tracking.append_recommendation_session("user-1", ["a", "b"])
    tracking.append_recommendation_session("user-1", ["a", "c"])

    assert tracking.register_click("user-1", "a") is True

    logs = tracking.load_logs()
    assert len(logs) == 2

    first, second = logs[0], logs[1]
    assert "a" not in first.clicked_items
    assert second.clicked_items == ["a"]


def test_register_event_returns_false_when_item_missing(isolated_default_log_file):
    tracking.append_recommendation_session("user-1", ["a", "b"])
    assert tracking.register_click("user-1", "missing-item") is False
    assert tracking.register_completion("user-1", "missing-item") is False

