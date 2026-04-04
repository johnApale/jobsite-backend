from decimal import Decimal
from uuid import UUID

from pydantic import BaseModel, ConfigDict

from app.api.schemas.assessment import CriterionInput


class ApplicantInput(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    profile_skills: str | None = None
    resume_parsed_text: str | None = None
    resume_extracted_skills: str | None = None
    ai_parsed_content: str | None = None


class ScreeningEvaluateRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    criteria: list[CriterionInput]
    applicant: ApplicantInput


class CriterionScore(BaseModel):
    model_config = ConfigDict(exclude_none=True, populate_by_name=True)

    criterion_id: UUID
    criterion_name: str
    category: str
    weight: Decimal
    score: Decimal
    result: str
    reasoning: str


class ScreeningEvaluateResponse(BaseModel):
    model_config = ConfigDict(exclude_none=True, populate_by_name=True)

    breakdown: list[CriterionScore]
    overall_score: Decimal
