"""Message broker consumer handlers for the 4 async AI operations.

Each handler receives a deserialized message dict from the broker,
calls the existing service logic, and publishes a response message.
"""

import json
from datetime import UTC, datetime
from typing import Any
from uuid import uuid4

import structlog

from app.api.schemas.answer_scoring import AnswerInput, ScoreAnswersRequest
from app.api.schemas.feedback import FeedbackRequest
from app.api.schemas.resume import ResumeParseRequest
from app.api.schemas.screening import (
    ApplicantInput,
    CriterionInput,
    ScreeningEvaluateRequest,
)
from app.core.config import Settings, get_settings
from app.core.events.inbound import (
    AnswerScoringRequested,
    FeedbackGenerationRequested,
    ResumeParseRequested,
    ScreeningEvaluationRequested,
)
from app.core.events.outbound import (
    AnswersScored,
    FeedbackGenerated,
    ResumeParsed,
    ScreeningEvaluated,
)
from app.core.services.ai_logging_service import AiLoggingService
from app.core.services.resume_service import ResumeService
from app.core.services.screening_service import ScreeningService
from app.infrastructure.ai_providers import get_ai_provider
from app.infrastructure.db.session import get_session_factory
from app.infrastructure.messaging.connection import get_channel
from app.infrastructure.messaging.publisher import publish

logger = structlog.get_logger()

# MassTransit exchange names — must match the C# full type names
EXCHANGE_RESUME_PARSE_REQUESTED = "Jobsite.SharedKernel.Events:ResumeParseRequested"
EXCHANGE_SCREENING_EVALUATION_REQUESTED = "Jobsite.SharedKernel.Events:ScreeningEvaluationRequested"
EXCHANGE_ANSWER_SCORING_REQUESTED = "Jobsite.SharedKernel.Events:AnswerScoringRequested"
EXCHANGE_FEEDBACK_GENERATION_REQUESTED = "Jobsite.SharedKernel.Events:FeedbackGenerationRequested"

EXCHANGE_RESUME_PARSED = "Jobsite.SharedKernel.Events:ResumeParsed"
EXCHANGE_SCREENING_EVALUATED = "Jobsite.SharedKernel.Events:ScreeningEvaluated"
EXCHANGE_ANSWERS_SCORED = "Jobsite.SharedKernel.Events:AnswersScored"
EXCHANGE_FEEDBACK_GENERATED = "Jobsite.SharedKernel.Events:FeedbackGenerated"


async def handle_resume_parse(payload: dict[str, Any]) -> None:
    """Handle ResumeParseRequested — parse resume text and publish result."""
    event = ResumeParseRequested.model_validate(payload)
    settings: Settings = get_settings()
    session_factory = get_session_factory()

    async with session_factory() as session:
        provider = get_ai_provider(settings)
        logging_service = AiLoggingService(session)
        service = ResumeService(session, provider, logging_service)

        request = ResumeParseRequest(parsed_text=event.parsed_text)
        result = await service.parse(request, event.tenant_id)

        response = ResumeParsed(
            event_id=uuid4(),
            tenant_id=event.tenant_id,
            resume_id=event.resume_id,
            ai_parsed_content=result.model_dump_json(exclude_none=True),
            correlation_id=event.correlation_id,
            occurred_at=datetime.now(UTC),
        )

    channel = await get_channel()
    await publish(
        channel,
        EXCHANGE_RESUME_PARSED,
        response.model_dump(mode="json"),
        correlation_id=event.correlation_id,
    )

    await logger.ainfo(
        "Resume parse completed",
        resume_id=str(event.resume_id),
        correlation_id=event.correlation_id,
    )


async def handle_screening_evaluation(payload: dict[str, Any]) -> None:
    """Handle ScreeningEvaluationRequested — evaluate criteria and publish scores."""
    event = ScreeningEvaluationRequested.model_validate(payload)
    settings: Settings = get_settings()
    session_factory = get_session_factory()

    criteria_data: list[dict[str, Any]] = json.loads(event.criteria_json)
    applicant_data: dict[str, Any] = json.loads(event.applicant_data_json)

    request = ScreeningEvaluateRequest(
        criteria=[CriterionInput.model_validate(c) for c in criteria_data],
        applicant=ApplicantInput.model_validate(applicant_data),
    )

    async with session_factory() as session:
        provider = get_ai_provider(settings)
        logging_service = AiLoggingService(session)
        service = ScreeningService(session, provider, logging_service)

        result = await service.evaluate(request, event.tenant_id)

        response = ScreeningEvaluated(
            event_id=uuid4(),
            tenant_id=event.tenant_id,
            application_id=event.application_id,
            breakdown_json=result.model_dump_json(include={"breakdown"}),
            overall_score=result.overall_score,
            correlation_id=event.correlation_id,
            occurred_at=datetime.now(UTC),
        )

    channel = await get_channel()
    await publish(
        channel,
        EXCHANGE_SCREENING_EVALUATED,
        response.model_dump(mode="json"),
        correlation_id=event.correlation_id,
    )

    await logger.ainfo(
        "Screening evaluation completed",
        application_id=str(event.application_id),
        overall_score=str(result.overall_score),
        correlation_id=event.correlation_id,
    )


async def handle_answer_scoring(payload: dict[str, Any]) -> None:
    """Handle AnswerScoringRequested — score free-text answers and publish results."""
    event = AnswerScoringRequested.model_validate(payload)
    settings: Settings = get_settings()
    session_factory = get_session_factory()

    answers_data: list[dict[str, Any]] = json.loads(event.answers_json)
    request = ScoreAnswersRequest(
        answers=[AnswerInput.model_validate(a) for a in answers_data],
    )

    async with session_factory() as session:
        provider = get_ai_provider(settings)
        logging_service = AiLoggingService(session)
        service = ScreeningService(session, provider, logging_service)

        result = await service.score_answers(request, event.tenant_id)

        response = AnswersScored(
            event_id=uuid4(),
            tenant_id=event.tenant_id,
            application_id=event.application_id,
            scores_json=result.model_dump_json(),
            correlation_id=event.correlation_id,
            occurred_at=datetime.now(UTC),
        )

    channel = await get_channel()
    await publish(
        channel,
        EXCHANGE_ANSWERS_SCORED,
        response.model_dump(mode="json"),
        correlation_id=event.correlation_id,
    )

    await logger.ainfo(
        "Answer scoring completed",
        application_id=str(event.application_id),
        answers_count=len(answers_data),
        correlation_id=event.correlation_id,
    )


async def handle_feedback_generation(payload: dict[str, Any]) -> None:
    """Handle FeedbackGenerationRequested — generate feedback and publish result."""
    event = FeedbackGenerationRequested.model_validate(payload)
    settings: Settings = get_settings()
    session_factory = get_session_factory()

    request = FeedbackRequest(
        criteria_breakdown=event.criteria_breakdown,
        overall_score=event.overall_score,
        transparency_level=event.transparency_level,
    )

    async with session_factory() as session:
        provider = get_ai_provider(settings)
        logging_service = AiLoggingService(session)
        service = ScreeningService(session, provider, logging_service)

        result = await service.generate_feedback(request, event.tenant_id)

        response = FeedbackGenerated(
            event_id=uuid4(),
            tenant_id=event.tenant_id,
            application_id=event.application_id,
            feedback=result.feedback,
            correlation_id=event.correlation_id,
            occurred_at=datetime.now(UTC),
        )

    channel = await get_channel()
    await publish(
        channel,
        EXCHANGE_FEEDBACK_GENERATED,
        response.model_dump(mode="json"),
        correlation_id=event.correlation_id,
    )

    await logger.ainfo(
        "Feedback generation completed",
        application_id=str(event.application_id),
        transparency_level=event.transparency_level,
        correlation_id=event.correlation_id,
    )
