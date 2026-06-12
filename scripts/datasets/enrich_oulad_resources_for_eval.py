#!/usr/bin/env python3
"""
OULAD VLE rows expose opaque site ids in titles; cosine-based content signals
(TF-IDF and sentence embeddings) work better when titles and descriptions read
like learning materials rather than database keys.
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Any, Callable

ROOT_DIR = Path(__file__).resolve().parents[2]
DEFAULT_PROCESSED_DIR = ROOT_DIR / "datasets" / "oulad" / "processed"

MODULE_DISPLAY_NAMES: dict[str, str] = {
    "AAA": "Arts and Humanities",
    "BBB": "Social Sciences",
    "CCC": "Computing and Technology",
    "DDD": "Social Sciences",
    "EEE": "Environmental Science",
    "FFF": "Mathematics and Statistics",
    "GGG": "Psychology",
    "HHH": "Science and Health",
}

MODULE_SUBJECT_KEYWORDS: dict[str, str] = {
    "AAA": "arts humanities culture history interpretation creative writing critical reading",
    "BBB": "social sciences society citizenship policy qualitative research evidence",
    "CCC": "computing technology programming software systems digital literacy problem solving",
    "DDD": "social policy welfare community research methods qualitative analysis",
    "EEE": "environmental science ecology sustainability climate systems field study",
    "FFF": "mathematics statistics quantitative reasoning modelling problem sets proofs",
    "GGG": "psychology cognition behaviour mental health research methods experiments",
    "HHH": "science health biology chemistry laboratory skills scientific method",
}

ACTIVITY_PEDAGOGICAL_ROLE: dict[str, str] = {
    "oucontent": "core guided study unit with primary teaching content",
    "resource": "supplementary reading reference and extension material",
    "subpage": "topic section overview and structured module navigation",
    "sharedsubpage": "shared topic section for collaborative module work",
    "page": "course information page and module orientation",
    "homepage": "course entry point and weekly study overview",
    "folder": "grouped learning materials and resource collection",
    "url": "external reference link and further reading",
    "forumng": "discussion forum peer exchange and tutor-led debate",
    "oucollaborate": "live collaborative tutorial and group activity",
    "ouwiki": "collaborative course wiki knowledge building",
    "ouelluminate": "recorded live tutorial session and seminar replay",
    "quiz": "formative practice quiz and knowledge check",
    "externalquiz": "external assessment practice and skills drill",
    "questionnaire": "reflective questionnaire and self-assessment",
    "glossary": "key terms glossary and concept definitions",
    "dataplus": "data investigation activity and structured dataset task",
    "dualpane": "interactive split-view learning activity",
    "htmlactivity": "interactive HTML learning exercise",
    "repeatactivity": "repeatable practice activity for skill consolidation",
}

CONTENT_FORMAT_PHRASES: dict[str, str] = {
    "Article": "text-based reading material article notes and written guidance",
    "Video": "multimedia video lecture demonstration and audiovisual study content",
    "Quiz": "interactive quiz assessment questions and practice exercises",
}

DIFFICULTY_LABELS: dict[int, str] = {
    1: "introductory level suitable for new learners",
    2: "developing level building on foundational concepts",
    3: "intermediate level requiring prior module knowledge",
    4: "advanced level with demanding concepts",
    5: "challenging level for consolidation and extension",
}

_SITE_ID_SUFFIX = re.compile(r"\s*#\d+\s*$")
_MULTI_SPACE = re.compile(r"\s+")
_WEEK_NUM = re.compile(r"week\s*(\d+)", re.IGNORECASE)


def _clean_title(title: str) -> str:
    cleaned = _SITE_ID_SUFFIX.sub("", (title or "").strip())
    return _MULTI_SPACE.sub(" ", cleaned).strip()


def _module_display(code: str) -> str:
    code = (code or "").strip().upper()
    label = MODULE_DISPLAY_NAMES.get(code)
    if label:
        return f"{label} ({code})"
    return code or "Open University module"


def _week_number(week_text: str) -> int | None:
    match = _WEEK_NUM.search(week_text or "")
    return int(match.group(1)) if match else None


def _timeline_phrase(week: str, phase: str, presentation_length_days: int) -> str:
    parts: list[str] = []
    week_num = _week_number(week)
    if week_num is not None:
        approx_weeks = max(1, round(presentation_length_days / 7)) if presentation_length_days > 0 else 30
        parts.append(f"module week {week_num} of about {approx_weeks}")
    elif week:
        parts.append(week.lower())
    if phase and phase.lower() != "none":
        phase_map = {
            "early": "beginning foundation stage of the presentation",
            "mid-course": "middle progression stage of the presentation",
            "late": "final consolidation stage before assessment",
        }
        parts.append(phase_map.get(phase.lower(), f"{phase.lower()} stage"))
    return "; ".join(parts)


def enrich_resource_v2(resource: dict[str, Any]) -> dict[str, Any]:
    out = dict(resource)
    module_code = str(resource.get("codeModule") or resource.get("topic") or "").strip()
    module_label = _module_display(module_code)
    activity = str(resource.get("activityLabel") or "Learning activity").strip()
    week = str(resource.get("weekInferred") or resource.get("week") or "").strip()
    phase = str(resource.get("coursePhase") or "").strip()
    presentation = str(resource.get("presentation") or "").strip()
    content_type = str(resource.get("contentType") or "Article").strip()
    learners = int(resource.get("uniqueLearners") or 0)
    clicks = int(resource.get("totalClicks") or 0)

    title_parts: list[str] = []
    if week:
        title_parts.append(week)
    title_parts.append(activity)
    title_parts.append(f"{module_label} course")
    out["title"] = _clean_title(" — ".join(title_parts))[:200]

    desc_bits = [f"{activity} for the {module_label} module"]
    if presentation:
        desc_bits.append(f"presentation {presentation}")
    if week:
        desc_bits.append(f"scheduled around {week.lower()}")
    if phase:
        desc_bits.append(f"during the {phase.lower()} part of the course")
    desc_bits.append(f"delivered as {content_type.lower()} material")
    if learners > 0 or clicks > 0:
        desc_bits.append(
            f"historically used by about {learners} learners with {clicks} recorded clicks"
        )
    out["description"] = (_MULTI_SPACE.sub(" ", ". ".join(desc_bits)) + ".")[:1000]

    out["topicName"] = module_label
    out["learningContext"] = (
        f"{module_label}; {activity}; {week or 'general timeline'}; {phase or 'course'} phase"
    )[:300]
    out["enrichmentProfile"] = "v2"
    return out


def enrich_resource_v3(resource: dict[str, Any]) -> dict[str, Any]:
    out = enrich_resource_v2(resource)
    module_code = str(resource.get("codeModule") or resource.get("topic") or "").strip().upper()
    module_label = out["topicName"]
    activity_type = str(resource.get("activityType") or "").strip().lower()
    activity = str(resource.get("activityLabel") or "Learning activity").strip()
    week = str(resource.get("weekInferred") or resource.get("week") or "").strip()
    phase = str(resource.get("coursePhase") or "").strip()
    content_type = str(resource.get("contentType") or "Article").strip()
    difficulty = int(resource.get("difficulty") or 3)
    presentation_length = int(resource.get("presentationLengthDays") or 0)

    pedagogical_role = ACTIVITY_PEDAGOGICAL_ROLE.get(
        activity_type,
        f"virtual learning environment activity focused on {activity.lower()}",
    )
    content_format = CONTENT_FORMAT_PHRASES.get(content_type, f"{content_type.lower()} learning material")
    difficulty_label = DIFFICULTY_LABELS.get(difficulty, DIFFICULTY_LABELS[3])
    subject_keywords = MODULE_SUBJECT_KEYWORDS.get(module_code, module_label.lower())
    timeline = _timeline_phrase(week, phase, presentation_length)

    out["title"] = _clean_title(
        " — ".join(filter(None, [week, activity, module_label]))
    )[:200]
    desc_bits = [
        f"{activity} in the {module_label} open university module",
        pedagogical_role,
        content_format,
        difficulty_label,
    ]
    if timeline:
        desc_bits.append(f"positioned at {timeline}")
    desc_bits.append(f"subject focus: {subject_keywords}")
    out["description"] = (_MULTI_SPACE.sub(" ", ". ".join(desc_bits)) + ".")[:1200]
    out["pedagogicalRole"] = pedagogical_role
    out["contentFormat"] = content_format
    out["difficultyLabel"] = difficulty_label
    out["semanticKeywords"] = " ".join(
        filter(
            None,
            [
                module_code.lower(),
                activity_type,
                activity.lower(),
                subject_keywords,
                content_type.lower(),
            ],
        )
    )[:500]
    out["learningContext"] = (
        f"{module_label}; {activity}; {pedagogical_role}; {timeline or 'general timeline'}"
    )[:400]
    out["enrichmentProfile"] = "v3"
    return out


PROFILES: dict[str, Callable[[dict[str, Any]], dict[str, Any]]] = {
    "v2": enrich_resource_v2,
    "v3": enrich_resource_v3,
}


def main() -> None:
    parser = argparse.ArgumentParser(description="Enrich OULAD resource text for offline evaluation.")
    parser.add_argument("--processed-dir", type=Path, default=DEFAULT_PROCESSED_DIR)
    parser.add_argument("--profile", choices=tuple(PROFILES), default="v2")
    parser.add_argument("--input", type=Path, default=None)
    parser.add_argument("--output", type=Path, default=None)
    args = parser.parse_args()

    processed_dir = args.processed_dir
    input_path = args.input or (processed_dir / "resources.json")
    output_path = args.output or (processed_dir / "resources_enriched.json")
    enrich = PROFILES[args.profile]

    resources = json.loads(input_path.read_text(encoding="utf-8"))
    enriched = [enrich(r) for r in resources]
    output_path.write_text(json.dumps(enriched, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Wrote {len(enriched)} resources (profile {args.profile}) → {output_path}")


if __name__ == "__main__":
    main()
