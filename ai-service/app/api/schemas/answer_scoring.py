from decimal import Decimal
from uuid import UUID

from pydantic import BaseModel, ConfigDict


class AnswerInput(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    question_id: UUID
    question_text: str
    response_text: str
    scoring_guidance: str | None = None
    key_topics: list[str] | None = None


class ScoreAnswersRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    answers: list[AnswerInput]


class AnswerScoreResult(BaseModel):
    model_config = ConfigDict(exclude_none=True, populate_by_name=True)

    question_id: UUID
    score: Decimal
    result: str
    reasoning: str


class ScoreAnswersResponse(BaseModel):
    model_config = ConfigDict(exclude_none=True, populate_by_name=True)

    scores: list[AnswerScoreResult]
