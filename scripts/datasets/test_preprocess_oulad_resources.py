"""Tests for OULAD resource metadata fields in preprocessing."""

import sys
from pathlib import Path

SCRIPTS_DATASETS = Path(__file__).resolve().parent
ROOT = SCRIPTS_DATASETS.parents[1]
for path in (str(ROOT / "ai-service"), str(SCRIPTS_DATASETS)):
    if path not in sys.path:
        sys.path.insert(0, path)

from preprocess_oulad import (  # noqa: E402
    ResourceStats,
    activity_label,
    build_engagement_and_temporal_fields,
    build_resource_metadata_fields,
    content_type_from_activity,
    course_phase_from_week_number,
    difficulty_from_activity,
    infer_week_number,
    stable_resource_id,
)
from recommender.content_semantic import build_resource_semantic_text  # noqa: E402


def test_build_resource_metadata_fields_includes_structured_values():
    fields = build_resource_metadata_fields(
        code_module="AAA",
        code_presentation="2013J",
        activity_type="oucontent",
        week_from=5,
        week_to=5,
    )
    assert fields == {
        "codeModule": "AAA",
        "presentation": "2013J",
        "activityType": "oucontent",
        "activityLabel": "Course study material",
        "week": "Week 5",
    }


def test_build_resource_metadata_fields_omits_empty_week():
    fields = build_resource_metadata_fields(
        code_module="BBB",
        code_presentation="2013B",
        activity_type="resource",
        week_from=0,
        week_to=0,
    )
    assert "week" not in fields
    assert fields["activityType"] == "resource"
    assert fields["presentation"] == "2013B"


def test_stable_resource_id_unchanged():
    rid = stable_resource_id("AAA", "2013J", 546712)
    assert rid == stable_resource_id("AAA", "2013J", 546712)
    assert len(rid) == 36


def test_infer_week_number_and_course_phase_are_deterministic():
    assert infer_week_number(129, 240) == 16
    assert infer_week_number(129, 240) == 16
    assert course_phase_from_week_number(16) == "Mid-course"
    assert course_phase_from_week_number(5) == "Early"
    assert course_phase_from_week_number(25) == "Late"


def test_engagement_fields_infer_week_when_explicit_week_missing():
    stat = ResourceStats(
        total_clicks=1000,
        count_pairs=50,
        min_date=-10,
        max_date=268,
        weighted_day_sum=129_000.0,
    )
    fields = build_engagement_and_temporal_fields(
        code_module="BBB",
        code_presentation="2013B",
        stat=stat,
        presentation_lengths={("BBB", "2013B"): 240},
        explicit_week_num=0,
    )
    assert fields["presentationLengthDays"] == 240
    assert fields["totalClicks"] == 1000
    assert fields["uniqueLearners"] == 50
    assert fields["activeFromDay"] == -10
    assert fields["activeToDay"] == 268
    assert fields["typicalDayOffset"] == 129
    assert fields["weekInferred"] == "Week 16"
    assert fields["coursePhase"] == "Mid-course"
    assert "week" not in fields


def test_engagement_fields_keep_explicit_week_without_week_inferred():
    stat = ResourceStats(total_clicks=500, count_pairs=10, min_date=0, max_date=100, weighted_day_sum=2500.0)
    fields = build_engagement_and_temporal_fields(
        code_module="AAA",
        code_presentation="2013J",
        stat=stat,
        presentation_lengths={("AAA", "2013J"): 268},
        explicit_week_num=5,
    )
    assert "weekInferred" not in fields
    assert fields["coursePhase"] == "Early"


def test_expanded_activity_type_mappings():
    assert activity_label("url") == "External link"
    assert content_type_from_activity("url") == "Article"
    assert difficulty_from_activity("url") == 1
    assert activity_label("oucollaborate") == "Collaborative activity"
    assert content_type_from_activity("questionnaire") == "Quiz"
    assert difficulty_from_activity("dataplus") == 5


def test_semantic_builder_consumes_inferred_week_activity_label_and_phase():
    resource = {
        "id": stable_resource_id("BBB", "2013B", 543306),
        "title": "External link - Module BBB",
        "topic": "BBB",
        "description": "External link from the OULAD virtual learning environment.",
        "difficulty": 1,
        "estimatedDurationMinutes": 5,
        "contentType": "Article",
        "codeModule": "BBB",
        "presentation": "2013B",
        "activityType": "url",
        "activityLabel": "External link",
        "weekInferred": "Week 16",
        "coursePhase": "Mid-course",
    }
    text = build_resource_semantic_text(resource)
    assert "Week: Week 16" in text
    assert "Phase: Mid-course" in text
    assert "Activity: External link" in text
    assert "Activity: url" not in text


def test_semantic_builder_prefers_explicit_week_over_inferred():
    resource = {
        "id": stable_resource_id("AAA", "2013J", 546712),
        "title": "Week 5 Course study material - Module AAA",
        "topic": "AAA",
        "description": "Course study material.",
        "contentType": "Video",
        "week": "Week 5",
        "weekInferred": "Week 12",
        "activityLabel": "Course study material",
        "activityType": "oucontent",
    }
    text = build_resource_semantic_text(resource)
    assert "Week: Week 5" in text
    assert "Week 12" not in text
