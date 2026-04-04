from decimal import Decimal

from pydantic import BaseModel, ConfigDict


class FeedbackRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    criteria_breakdown: str
    overall_score: Decimal
    transparency_level: str


class FeedbackResponse(BaseModel):
    model_config = ConfigDict(exclude_none=True, populate_by_name=True)

    feedback: str
