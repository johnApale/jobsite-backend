from pydantic import BaseModel, ConfigDict


class ResumeParseRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    parsed_text: str


class ExtractedSkill(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    name: str
    level: str | None = None
    years: int | None = None


class Experience(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    title: str
    company: str
    start_date: str | None = None
    end_date: str | None = None
    description: str | None = None


class Education(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    degree: str
    institution: str
    start_date: str | None = None
    end_date: str | None = None
    field: str | None = None


class ResumeParseResponse(BaseModel):
    model_config = ConfigDict(exclude_none=True, populate_by_name=True)

    skills: list[ExtractedSkill] | None = None
    experience: list[Experience] | None = None
    education: list[Education] | None = None
    certifications: list[str] | None = None
    summary: str | None = None
