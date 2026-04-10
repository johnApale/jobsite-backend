import uuid
from decimal import Decimal

from app.api.schemas.assessment import QuestionSuggestion
from app.api.schemas.criteria import CriteriaSuggestion
from app.api.schemas.feedback import FeedbackResponse
from app.api.schemas.resume import Education, Experience, ExtractedSkill, ResumeParseResponse
from app.api.schemas.screening import (
    CriterionScore,
)


def test_resume_parse_response_excludes_none_fields():
    response = ResumeParseResponse(skills=None, experience=None, summary="A summary")
    data = response.model_dump(exclude_none=True)
    assert "skills" not in data
    assert "experience" not in data
    assert data["summary"] == "A summary"


def test_resume_parse_response_includes_all_populated_fields():
    response = ResumeParseResponse(
        skills=[ExtractedSkill(name="Python", level="Advanced", years=5)],
        experience=[Experience(title="Dev", company="Co")],
        education=[Education(degree="BSc", institution="Uni")],
        certifications=["AWS"],
        summary="Full profile",
    )
    data = response.model_dump(exclude_none=True)
    assert "skills" in data
    assert "experience" in data
    assert "education" in data
    assert "certifications" in data
    assert "summary" in data


def test_criteria_suggestion_uses_snake_case():
    suggestion = CriteriaSuggestion(
        name="Python",
        category="Skill",
        evaluation_method="SemanticSimilarity",
        is_required=True,
        weight=Decimal("50.0"),
        configuration="{}",
    )
    data = suggestion.model_dump()
    assert "evaluation_method" in data
    assert "is_required" in data


def test_criterion_score_uses_pascal_case_result():
    score = CriterionScore(
        criterion_id=uuid.uuid4(),
        criterion_name="Python",
        category="Skill",
        weight=Decimal("50.0"),
        score=Decimal("85.0"),
        result="Pass",
        reasoning="Good match",
    )
    assert score.result in ("Pass", "Fail", "Required")


def test_question_suggestion_optional_fields_default_none():
    suggestion = QuestionSuggestion(
        question_text="Describe experience",
        question_type="FreeText",
        timing="AfterScreening",
        is_required=True,
        weight=Decimal("50.0"),
    )
    assert suggestion.expected_answer is None
    assert suggestion.options is None


def test_feedback_response_serialization():
    response = FeedbackResponse(feedback="Great job!")
    data = response.model_dump(exclude_none=True)
    assert data == {"feedback": "Great job!"}
