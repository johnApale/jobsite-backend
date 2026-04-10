import uuid
from decimal import Decimal
from unittest.mock import MagicMock

import pytest

from app.api.schemas.answer_scoring import AnswerInput, ScoreAnswersRequest
from app.api.schemas.assessment import AssessmentSuggestRequest, CriterionInput
from app.api.schemas.criteria import CriteriaSuggestRequest
from app.api.schemas.feedback import FeedbackRequest
from app.api.schemas.resume import ResumeParseRequest
from app.api.schemas.screening import ApplicantInput, ScreeningEvaluateRequest
from app.core.services.ai_logging_service import AiLoggingService, _estimate_cost
from app.core.services.assessment_service import AssessmentService
from app.core.services.criteria_service import CriteriaService
from app.core.services.resume_service import ResumeService
from app.core.services.screening_service import ScreeningService
from app.infrastructure.ai_providers.base import AiCompletionResult

# --- AI Logging Service ---


async def test_log_call_success_inserts_log_entry(mock_db_session, mock_ai_provider, sample_ai_result):
    service = AiLoggingService(mock_db_session)
    tenant_id = uuid.uuid4()

    await service.log_call(
        tenant_id=tenant_id,
        call_type="ResumeParsing",
        ai_provider=mock_ai_provider,
        latency_ms=250,
        http_status_code=200,
        is_success=True,
        result=sample_ai_result,
    )

    mock_db_session.add.assert_called_once()
    mock_db_session.flush.assert_awaited_once()


async def test_log_call_computes_estimated_cost():
    cost = _estimate_cost("gpt-4o", input_tokens=1000, output_tokens=500)
    assert cost is not None
    assert cost > Decimal("0")


async def test_log_call_unknown_model_returns_none_cost():
    cost = _estimate_cost("unknown-model", input_tokens=1000, output_tokens=500)
    assert cost is None


async def test_log_call_failure_records_error_message(mock_db_session, mock_ai_provider):
    service = AiLoggingService(mock_db_session)

    await service.log_call(
        tenant_id=uuid.uuid4(),
        call_type="ResumeParsing",
        ai_provider=mock_ai_provider,
        latency_ms=100,
        http_status_code=502,
        is_success=False,
        error_message="Connection timeout",
    )

    mock_db_session.add.assert_called_once()
    log_entry = mock_db_session.add.call_args[0][0]
    assert log_entry.is_success is False
    assert log_entry.error_message == "Connection timeout"


# --- Resume Service ---


async def test_parse_resume_valid_text_returns_structured_data(mock_db_session, mock_ai_provider):
    mock_ai_provider.complete.return_value = AiCompletionResult(
        content=(
            '{"skills": [{"name": "Python", "level": "Advanced", "years": 5}],'
            ' "experience": [], "education": [], "certifications": [],'
            ' "summary": "Experienced developer"}'
        ),
        input_tokens=100,
        output_tokens=50,
        total_tokens=150,
    )

    mock_db_session.execute.return_value = MagicMock(scalar_one_or_none=MagicMock(return_value=None))

    logging_service = AiLoggingService(mock_db_session)
    service = ResumeService(mock_db_session, mock_ai_provider, logging_service)

    result = await service.parse(ResumeParseRequest(parsed_text="I have 5 years of Python experience"), uuid.uuid4())

    assert result.skills is not None
    assert len(result.skills) == 1
    assert result.skills[0].name == "Python"
    assert result.skills[0].years == 5
    mock_ai_provider.complete.assert_awaited_once()


async def test_parse_resume_cached_result_returns_cache_hit(mock_db_session, mock_ai_provider):
    cached_row = MagicMock()
    cached_row.parsed_result = {
        "skills": [{"name": "Java", "level": "Expert", "years": 10}],
        "summary": "Cached result",
    }

    mock_db_session.execute.return_value = MagicMock(scalar_one_or_none=MagicMock(return_value=cached_row))

    logging_service = AiLoggingService(mock_db_session)
    service = ResumeService(mock_db_session, mock_ai_provider, logging_service)

    result = await service.parse(ResumeParseRequest(parsed_text="Some resume text"), uuid.uuid4())

    assert result.summary == "Cached result"
    mock_ai_provider.complete.assert_not_awaited()


async def test_parse_resume_stores_result_in_cache(mock_db_session, mock_ai_provider):
    mock_ai_provider.complete.return_value = AiCompletionResult(
        content='{"skills": [], "experience": [], "education": [], "certifications": [], "summary": "New parse"}',
        input_tokens=100,
        output_tokens=50,
        total_tokens=150,
    )
    mock_db_session.execute.return_value = MagicMock(scalar_one_or_none=MagicMock(return_value=None))

    logging_service = AiLoggingService(mock_db_session)
    service = ResumeService(mock_db_session, mock_ai_provider, logging_service)

    await service.parse(ResumeParseRequest(parsed_text="Resume content"), uuid.uuid4())

    # add called twice: once for log entry, once for cache entry
    assert mock_db_session.add.call_count == 2


async def test_parse_resume_ai_failure_raises(mock_db_session, mock_ai_provider):
    from app.core.errors import AppError

    mock_ai_provider.complete.side_effect = AppError(code="AI_PROVIDER_ERROR", status_code=502, message="Provider down")
    mock_db_session.execute.return_value = MagicMock(scalar_one_or_none=MagicMock(return_value=None))

    logging_service = AiLoggingService(mock_db_session)
    service = ResumeService(mock_db_session, mock_ai_provider, logging_service)

    with pytest.raises(AppError, match="Provider down"):
        await service.parse(ResumeParseRequest(parsed_text="text"), uuid.uuid4())


# --- Criteria Service ---


async def test_suggest_criteria_valid_job_returns_suggestions(mock_db_session, mock_ai_provider):
    mock_ai_provider.complete.return_value = AiCompletionResult(
        content=(
            '{"suggestions": [{"name": "C# Proficiency",'
            ' "category": "Skill",'
            ' "evaluation_method": "SemanticSimilarity",'
            ' "is_required": true, "weight": 25.0,'
            ' "configuration": "{}"}]}'
        ),
        input_tokens=200,
        output_tokens=100,
        total_tokens=300,
    )

    logging_service = AiLoggingService(mock_db_session)
    service = CriteriaService(mock_db_session, mock_ai_provider, logging_service)

    result = await service.suggest(
        CriteriaSuggestRequest(job_title="Developer", job_description="Need a C# expert"),
        uuid.uuid4(),
    )

    assert len(result) == 1
    assert result[0].name == "C# Proficiency"
    assert result[0].category == "Skill"


async def test_suggest_criteria_ai_failure_raises(mock_db_session, mock_ai_provider):
    from app.core.errors import AppError

    mock_ai_provider.complete.side_effect = AppError(code="AI_PROVIDER_ERROR", status_code=502, message="Timeout")

    logging_service = AiLoggingService(mock_db_session)
    service = CriteriaService(mock_db_session, mock_ai_provider, logging_service)

    with pytest.raises(AppError):
        await service.suggest(
            CriteriaSuggestRequest(job_title="Dev", job_description="Description"),
            uuid.uuid4(),
        )


# --- Assessment Service ---


async def test_suggest_questions_valid_input_returns_questions(mock_db_session, mock_ai_provider):
    mock_ai_provider.complete.return_value = AiCompletionResult(
        content=(
            '{"suggestions": [{"question_text": "Describe your C# experience",'
            ' "question_type": "FreeText", "timing": "AfterScreening",'
            ' "is_required": true, "weight": 50.0,'
            ' "expected_answer": "{\\"key_topics\\": [\\"async\\"]}",'
            ' "options": null}]}'
        ),
        input_tokens=200,
        output_tokens=100,
        total_tokens=300,
    )

    logging_service = AiLoggingService(mock_db_session)
    service = AssessmentService(mock_db_session, mock_ai_provider, logging_service)

    criteria = [
        CriterionInput(
            id=uuid.uuid4(),
            name="C#",
            category="Skill",
            evaluation_method="SemanticSimilarity",
            is_required=True,
            weight=Decimal("50"),
            configuration="{}",
        )
    ]
    result = await service.suggest(
        AssessmentSuggestRequest(job_description="Need C# dev", criteria=criteria),
        uuid.uuid4(),
    )

    assert len(result) == 1
    assert result[0].question_type == "FreeText"
    assert result[0].expected_answer is not None


async def test_suggest_questions_multichoice_includes_options(mock_db_session, mock_ai_provider):
    mock_ai_provider.complete.return_value = AiCompletionResult(
        content=(
            '{"suggestions": [{"question_text": "Which framework?",'
            ' "question_type": "MultipleChoice",'
            ' "timing": "AfterScreening", "is_required": true,'
            ' "weight": 30.0,'
            ' "expected_answer": "{\\"correct_index\\": 0}",'
            ' "options": "[{\\"text\\": \\"ASP.NET\\"}, {\\"text\\": \\"Django\\"}]"}]}'
        ),
        input_tokens=200,
        output_tokens=100,
        total_tokens=300,
    )

    logging_service = AiLoggingService(mock_db_session)
    service = AssessmentService(mock_db_session, mock_ai_provider, logging_service)

    criteria = [
        CriterionInput(
            id=uuid.uuid4(),
            name="Framework",
            category="Skill",
            evaluation_method="ExactMatch",
            is_required=True,
            weight=Decimal("50"),
            configuration="{}",
        )
    ]
    result = await service.suggest(
        AssessmentSuggestRequest(job_description="Web dev", criteria=criteria),
        uuid.uuid4(),
    )

    assert result[0].options is not None
    assert "ASP.NET" in result[0].options


# --- Screening Service ---


async def test_evaluate_valid_input_returns_per_criterion_scores(mock_db_session, mock_ai_provider):
    criterion_id = str(uuid.uuid4())
    mock_ai_provider.complete.return_value = AiCompletionResult(
        content=(
            f'{{"breakdown": [{{"criterion_id": "{criterion_id}",'
            f' "criterion_name": "Python", "category": "Skill",'
            f' "weight": 50.0, "score": 85.0, "result": "Pass",'
            f' "reasoning": "Strong match"}}],'
            f' "overall_score": 85.0}}'
        ),
        input_tokens=300,
        output_tokens=150,
        total_tokens=450,
    )

    logging_service = AiLoggingService(mock_db_session)
    service = ScreeningService(mock_db_session, mock_ai_provider, logging_service)

    criteria = [
        CriterionInput(
            id=uuid.UUID(criterion_id),
            name="Python",
            category="Skill",
            evaluation_method="SemanticSimilarity",
            is_required=True,
            weight=Decimal("50"),
            configuration="{}",
        )
    ]
    result = await service.evaluate(
        ScreeningEvaluateRequest(
            criteria=criteria,
            applicant=ApplicantInput(resume_parsed_text="5 years of Python"),
        ),
        uuid.uuid4(),
    )

    assert len(result.breakdown) == 1
    assert result.breakdown[0].result == "Pass"
    assert result.overall_score == Decimal("85.0")


async def test_score_answers_valid_input_returns_answer_scores(mock_db_session, mock_ai_provider):
    question_id = str(uuid.uuid4())
    mock_ai_provider.complete.return_value = AiCompletionResult(
        content=(
            f'{{"scores": [{{"question_id": "{question_id}",'
            f' "score": 72.0, "result": "Pass",'
            f' "reasoning": "Good coverage"}}]}}'
        ),
        input_tokens=200,
        output_tokens=80,
        total_tokens=280,
    )

    logging_service = AiLoggingService(mock_db_session)
    service = ScreeningService(mock_db_session, mock_ai_provider, logging_service)

    result = await service.score_answers(
        ScoreAnswersRequest(
            answers=[
                AnswerInput(
                    question_id=uuid.UUID(question_id),
                    question_text="Describe your experience",
                    response_text="I have 5 years of experience",
                )
            ]
        ),
        uuid.uuid4(),
    )

    assert len(result.scores) == 1
    assert result.scores[0].result == "Pass"


async def test_generate_feedback_full_transparency_returns_detailed(mock_db_session, mock_ai_provider):
    mock_ai_provider.complete.return_value = AiCompletionResult(
        content=(
            '{"feedback": "Based on your application, you demonstrated strong'
            " skills in Python and Django. Your experience with REST APIs was"
            " particularly noteworthy. Area for improvement: cloud deployment"
            ' experience."}'
        ),
        input_tokens=200,
        output_tokens=100,
        total_tokens=300,
    )

    logging_service = AiLoggingService(mock_db_session)
    service = ScreeningService(mock_db_session, mock_ai_provider, logging_service)

    result = await service.generate_feedback(
        FeedbackRequest(
            criteria_breakdown='[{"name": "Python", "score": 85}]',
            overall_score=Decimal("85.0"),
            transparency_level="Full",
        ),
        uuid.uuid4(),
    )

    assert len(result.feedback) > 0
    assert "Python" in result.feedback


async def test_generate_feedback_summary_transparency_returns_concise(mock_db_session, mock_ai_provider):
    mock_ai_provider.complete.return_value = AiCompletionResult(
        content='{"feedback": "Your application shows strong technical skills with room for growth in some areas."}',
        input_tokens=150,
        output_tokens=40,
        total_tokens=190,
    )

    logging_service = AiLoggingService(mock_db_session)
    service = ScreeningService(mock_db_session, mock_ai_provider, logging_service)

    result = await service.generate_feedback(
        FeedbackRequest(
            criteria_breakdown='[{"name": "Python", "score": 85}]',
            overall_score=Decimal("85.0"),
            transparency_level="Summary",
        ),
        uuid.uuid4(),
    )

    assert len(result.feedback) > 0
