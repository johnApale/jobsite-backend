"""Outbound event schemas — messages published to the monolith."""

from datetime import datetime
from decimal import Decimal
from uuid import UUID

from pydantic import BaseModel, ConfigDict


class ResumeParsed(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    event_id: UUID
    tenant_id: UUID
    resume_id: UUID
    ai_parsed_content: str
    correlation_id: str
    occurred_at: datetime


class ScreeningEvaluated(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    event_id: UUID
    tenant_id: UUID
    application_id: UUID
    breakdown_json: str
    overall_score: Decimal
    correlation_id: str
    occurred_at: datetime


class AnswersScored(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    event_id: UUID
    tenant_id: UUID
    application_id: UUID
    scores_json: str
    correlation_id: str
    occurred_at: datetime


class FeedbackGenerated(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    event_id: UUID
    tenant_id: UUID
    application_id: UUID
    feedback: str
    correlation_id: str
    occurred_at: datetime
