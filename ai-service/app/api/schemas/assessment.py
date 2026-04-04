from decimal import Decimal
from uuid import UUID

from pydantic import BaseModel, ConfigDict


class CriterionInput(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    id: UUID
    name: str
    category: str
    evaluation_method: str
    is_required: bool
    weight: Decimal
    configuration: str


class AssessmentSuggestRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    job_description: str
    criteria: list[CriterionInput]


class QuestionSuggestion(BaseModel):
    model_config = ConfigDict(exclude_none=True, populate_by_name=True)

    question_text: str
    question_type: str
    timing: str
    is_required: bool
    weight: Decimal
    expected_answer: str | None = None
    options: str | None = None
