"""Contract tests for broker message schemas.

Validates that Pydantic inbound/outbound event models correctly deserialize
snake_case JSON payloads matching the C# event contracts in SharedKernel.
These tests guard the C# ↔ Python serialization boundary — if a C# property
is renamed or a Python model drifts, these tests catch it.
"""

from datetime import datetime
from decimal import Decimal
from uuid import UUID

import pytest

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

# ── Fixtures: sample payloads matching MassTransit snake_case output ──


_EVENT_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
_TENANT_ID = "11111111-2222-3333-4444-555555555555"
_RESUME_ID = "22222222-3333-4444-5555-666666666666"
_APP_ID = "33333333-4444-5555-6666-777777777777"
_CORRELATION_ID = "corr-test-001"
_OCCURRED_AT = "2025-06-15T10:30:00Z"


# ──────────────────────────────────────────────────────────────────
# Inbound events (consumed from monolith)
# ──────────────────────────────────────────────────────────────────


class TestResumeParseRequestedContract:
    """Validates ResumeParseRequested deserialization from C# snake_case JSON."""

    @pytest.fixture()
    def payload(self) -> dict:
        return {
            "event_id": _EVENT_ID,
            "tenant_id": _TENANT_ID,
            "resume_id": _RESUME_ID,
            "parsed_text": "Experienced .NET developer with 5 years of C#.",
            "correlation_id": _CORRELATION_ID,
            "occurred_at": _OCCURRED_AT,
        }

    def test_deserializes_all_fields(self, payload: dict) -> None:
        event = ResumeParseRequested.model_validate(payload)

        assert event.event_id == UUID(_EVENT_ID)
        assert event.tenant_id == UUID(_TENANT_ID)
        assert event.resume_id == UUID(_RESUME_ID)
        assert event.parsed_text == payload["parsed_text"]
        assert event.correlation_id == _CORRELATION_ID
        assert isinstance(event.occurred_at, datetime)

    def test_rejects_missing_required_field(self, payload: dict) -> None:
        del payload["resume_id"]
        with pytest.raises(Exception):
            ResumeParseRequested.model_validate(payload)

    def test_round_trips_through_model_dump(self, payload: dict) -> None:
        event = ResumeParseRequested.model_validate(payload)
        dumped = event.model_dump(mode="json")

        assert dumped["event_id"] == _EVENT_ID
        assert dumped["resume_id"] == _RESUME_ID
        assert dumped["parsed_text"] == payload["parsed_text"]


class TestScreeningEvaluationRequestedContract:
    """Validates ScreeningEvaluationRequested deserialization."""

    @pytest.fixture()
    def payload(self) -> dict:
        return {
            "event_id": _EVENT_ID,
            "tenant_id": _TENANT_ID,
            "application_id": _APP_ID,
            "criteria_json": '[{"name":"C#","category":"Skill"}]',
            "applicant_data_json": '{"skills":["C#","SQL"]}',
            "correlation_id": _CORRELATION_ID,
            "occurred_at": _OCCURRED_AT,
        }

    def test_deserializes_all_fields(self, payload: dict) -> None:
        event = ScreeningEvaluationRequested.model_validate(payload)

        assert event.event_id == UUID(_EVENT_ID)
        assert event.application_id == UUID(_APP_ID)
        assert event.criteria_json == payload["criteria_json"]
        assert event.applicant_data_json == payload["applicant_data_json"]

    def test_rejects_missing_criteria_json(self, payload: dict) -> None:
        del payload["criteria_json"]
        with pytest.raises(Exception):
            ScreeningEvaluationRequested.model_validate(payload)


class TestAnswerScoringRequestedContract:
    """Validates AnswerScoringRequested deserialization."""

    @pytest.fixture()
    def payload(self) -> dict:
        return {
            "event_id": _EVENT_ID,
            "tenant_id": _TENANT_ID,
            "application_id": _APP_ID,
            "answers_json": '[{"question":"Why?","answer":"Because."}]',
            "correlation_id": _CORRELATION_ID,
            "occurred_at": _OCCURRED_AT,
        }

    def test_deserializes_all_fields(self, payload: dict) -> None:
        event = AnswerScoringRequested.model_validate(payload)

        assert event.event_id == UUID(_EVENT_ID)
        assert event.application_id == UUID(_APP_ID)
        assert event.answers_json == payload["answers_json"]

    def test_rejects_missing_answers_json(self, payload: dict) -> None:
        del payload["answers_json"]
        with pytest.raises(Exception):
            AnswerScoringRequested.model_validate(payload)


class TestFeedbackGenerationRequestedContract:
    """Validates FeedbackGenerationRequested deserialization."""

    @pytest.fixture()
    def payload(self) -> dict:
        return {
            "event_id": _EVENT_ID,
            "tenant_id": _TENANT_ID,
            "application_id": _APP_ID,
            "criteria_breakdown": '[{"criterion":"C#","score":90}]',
            "overall_score": 87.0,
            "transparency_level": "Detailed",
            "correlation_id": _CORRELATION_ID,
            "occurred_at": _OCCURRED_AT,
        }

    def test_deserializes_all_fields(self, payload: dict) -> None:
        event = FeedbackGenerationRequested.model_validate(payload)

        assert event.event_id == UUID(_EVENT_ID)
        assert event.application_id == UUID(_APP_ID)
        assert event.criteria_breakdown == payload["criteria_breakdown"]
        assert event.overall_score == Decimal("87.0")
        assert event.transparency_level == "Detailed"

    def test_rejects_missing_transparency_level(self, payload: dict) -> None:
        del payload["transparency_level"]
        with pytest.raises(Exception):
            FeedbackGenerationRequested.model_validate(payload)

    def test_overall_score_accepts_numeric_types(self, payload: dict) -> None:
        payload["overall_score"] = 92.5
        event = FeedbackGenerationRequested.model_validate(payload)
        assert event.overall_score == Decimal("92.5")


# ──────────────────────────────────────────────────────────────────
# Outbound events (published to monolith)
# ──────────────────────────────────────────────────────────────────


class TestResumeParsedContract:
    """Validates ResumeParsed serialization matches C# expectations."""

    @pytest.fixture()
    def payload(self) -> dict:
        return {
            "event_id": _EVENT_ID,
            "tenant_id": _TENANT_ID,
            "resume_id": _RESUME_ID,
            "ai_parsed_content": '{"skills":[{"name":"C#","level":"Advanced"}]}',
            "correlation_id": _CORRELATION_ID,
            "occurred_at": _OCCURRED_AT,
        }

    def test_deserializes_all_fields(self, payload: dict) -> None:
        event = ResumeParsed.model_validate(payload)

        assert event.event_id == UUID(_EVENT_ID)
        assert event.resume_id == UUID(_RESUME_ID)
        assert event.ai_parsed_content == payload["ai_parsed_content"]

    def test_serializes_to_snake_case(self, payload: dict) -> None:
        event = ResumeParsed.model_validate(payload)
        dumped = event.model_dump(mode="json")

        assert "ai_parsed_content" in dumped
        assert "AiParsedContent" not in dumped
        assert "resume_id" in dumped
        assert "ResumeId" not in dumped


class TestScreeningEvaluatedContract:
    """Validates ScreeningEvaluated serialization matches C# expectations."""

    @pytest.fixture()
    def payload(self) -> dict:
        return {
            "event_id": _EVENT_ID,
            "tenant_id": _TENANT_ID,
            "application_id": _APP_ID,
            "breakdown_json": '[{"criterion":"SQL","score":75}]',
            "overall_score": 78.25,
            "correlation_id": _CORRELATION_ID,
            "occurred_at": _OCCURRED_AT,
        }

    def test_deserializes_all_fields(self, payload: dict) -> None:
        event = ScreeningEvaluated.model_validate(payload)

        assert event.event_id == UUID(_EVENT_ID)
        assert event.application_id == UUID(_APP_ID)
        assert event.breakdown_json == payload["breakdown_json"]
        assert event.overall_score == Decimal("78.25")

    def test_serializes_to_snake_case(self, payload: dict) -> None:
        event = ScreeningEvaluated.model_validate(payload)
        dumped = event.model_dump(mode="json")

        assert "breakdown_json" in dumped
        assert "BreakdownJson" not in dumped
        assert "overall_score" in dumped
        assert "OverallScore" not in dumped


class TestAnswersScoredContract:
    """Validates AnswersScored serialization matches C# expectations."""

    @pytest.fixture()
    def payload(self) -> dict:
        return {
            "event_id": _EVENT_ID,
            "tenant_id": _TENANT_ID,
            "application_id": _APP_ID,
            "scores_json": '[{"question_id":"abc","score":80}]',
            "correlation_id": _CORRELATION_ID,
            "occurred_at": _OCCURRED_AT,
        }

    def test_deserializes_all_fields(self, payload: dict) -> None:
        event = AnswersScored.model_validate(payload)

        assert event.event_id == UUID(_EVENT_ID)
        assert event.application_id == UUID(_APP_ID)
        assert event.scores_json == payload["scores_json"]

    def test_serializes_to_snake_case(self, payload: dict) -> None:
        event = AnswersScored.model_validate(payload)
        dumped = event.model_dump(mode="json")

        assert "scores_json" in dumped
        assert "ScoresJson" not in dumped


class TestFeedbackGeneratedContract:
    """Validates FeedbackGenerated serialization matches C# expectations."""

    @pytest.fixture()
    def payload(self) -> dict:
        return {
            "event_id": _EVENT_ID,
            "tenant_id": _TENANT_ID,
            "application_id": _APP_ID,
            "feedback": "You demonstrated strong skills in C# and SQL.",
            "correlation_id": _CORRELATION_ID,
            "occurred_at": _OCCURRED_AT,
        }

    def test_deserializes_all_fields(self, payload: dict) -> None:
        event = FeedbackGenerated.model_validate(payload)

        assert event.event_id == UUID(_EVENT_ID)
        assert event.application_id == UUID(_APP_ID)
        assert event.feedback == payload["feedback"]

    def test_serializes_to_snake_case(self, payload: dict) -> None:
        event = FeedbackGenerated.model_validate(payload)
        dumped = event.model_dump(mode="json")

        assert "feedback" in dumped
        assert "Feedback" not in dumped


# ──────────────────────────────────────────────────────────────────
# MassTransit Envelope
# ──────────────────────────────────────────────────────────────────


class TestMassTransitEnvelopeExtraction:
    """Validates that the MassTransit envelope 'message' key extraction
    works correctly — mirroring the consumer.py _on_message logic."""

    def test_extract_payload_from_envelope(self) -> None:
        envelope: dict = {
            "messageId": "some-id",
            "correlationId": _CORRELATION_ID,
            "sentTime": _OCCURRED_AT,
            "message": {
                "event_id": _EVENT_ID,
                "tenant_id": _TENANT_ID,
                "resume_id": _RESUME_ID,
                "parsed_text": "Some text",
                "correlation_id": _CORRELATION_ID,
                "occurred_at": _OCCURRED_AT,
            },
        }

        payload: dict = envelope.get("message", envelope)
        event = ResumeParseRequested.model_validate(payload)

        assert event.event_id == UUID(_EVENT_ID)
        assert event.resume_id == UUID(_RESUME_ID)

    def test_handles_payload_without_envelope(self) -> None:
        raw_payload: dict = {
            "event_id": _EVENT_ID,
            "tenant_id": _TENANT_ID,
            "resume_id": _RESUME_ID,
            "parsed_text": "Direct payload",
            "correlation_id": _CORRELATION_ID,
            "occurred_at": _OCCURRED_AT,
        }

        payload: dict = raw_payload.get("message", raw_payload)
        event = ResumeParseRequested.model_validate(payload)

        assert event.parsed_text == "Direct payload"
