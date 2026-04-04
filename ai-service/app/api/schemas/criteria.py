from decimal import Decimal

from pydantic import BaseModel, ConfigDict


class CriteriaSuggestRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    job_title: str
    job_description: str


class CriteriaSuggestion(BaseModel):
    model_config = ConfigDict(exclude_none=True, populate_by_name=True)

    name: str
    category: str
    evaluation_method: str
    is_required: bool
    weight: Decimal
    configuration: str
