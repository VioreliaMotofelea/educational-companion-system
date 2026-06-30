"""Safety checks for OULAD content-mode experiment runner."""

import sys
from pathlib import Path

import pytest

SCRIPTS_DATASETS = Path(__file__).resolve().parent
ROOT = SCRIPTS_DATASETS.parents[1]
for path in (str(ROOT / "ai-service"), str(SCRIPTS_DATASETS)):
    if path not in sys.path:
        sys.path.insert(0, path)

from run_oulad_content_mode_experiments import (  # noqa: E402
    assert_rec_lists_complete,
    select_evaluated_users,
    validate_experiment_inputs,
)


def test_validate_experiment_inputs_rejects_empty_resources():
    with pytest.raises(SystemExit, match="resources.json is empty"):
        validate_experiment_inputs(k=10, resources=[])


def test_validate_experiment_inputs_rejects_nonpositive_k():
    with pytest.raises(SystemExit, match="--k must be > 0"):
        validate_experiment_inputs(k=0, resources=[{"id": "r1"}])


def test_select_evaluated_users_is_deterministic():
    users = ["u3", "u1", "u2"]
    relevant = {"u1": {"a"}, "u2": {"a", "b"}, "u3": {"a", "b", "c"}}
    first = select_evaluated_users(
        list(users),
        max_users=2,
        sort_by_test_size=True,
        relevant_by_user=relevant,
        user_sample_seed=None,
    )
    second = select_evaluated_users(
        list(users),
        max_users=2,
        sort_by_test_size=True,
        relevant_by_user=relevant,
        user_sample_seed=None,
    )
    assert first == second == ["u3", "u2"]


def test_assert_rec_lists_complete_passes_when_all_users_present():
    users = ["u1", "u2"]
    rec_lists = {
        "hybrid_full__tfidf_only": {"u1": ["a"], "u2": ["b"]},
        "popularity_baseline": {"u1": ["x"], "u2": ["y"]},
    }
    assert_rec_lists_complete(rec_lists, users, list(rec_lists.keys()))


def test_assert_rec_lists_complete_fails_on_missing_user():
    with pytest.raises(RuntimeError, match="missing"):
        assert_rec_lists_complete(
            {"exp_a": {"u1": []}},
            ["u1", "u2"],
            ["exp_a"],
        )
