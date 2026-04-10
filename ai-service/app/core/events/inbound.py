"""Inbound event schemas — messages consumed from the monolith."""

from datetime import datetime
from decimal import Decimal
from uuid import UUID

from pydantic import BaseModel, ConfigDict


class ResumeParseRequested(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    event_id: UUID
    tenant_id: UUID
    resume_id: UUID
    parsed_text: str
    correlation_id: str
    occurred_at: datetime


class ScreeningEvaluationRequested(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    event_id: UUID
    tenant_id: UUID
    application_id: UUID
    criteria_json: str
    applicant_data_json: str
    correlation_id: str
    occurred_at: datetime


class AnswerScoringRequested(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    event_id: UUID
    tenant_id: UUID
    application_id: UUID
    answers_json: str
    correlation_id: str
    occurred_at: datetime


class FeedbackGenerationRequested(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    event_id: UUID
    tenant_id: UUID
    application_id: UUID
    criteria_breakdown: str
    overall_score: Decimal
    transparency_level: str
    correlation_id: str
    occurred_at: datetime
