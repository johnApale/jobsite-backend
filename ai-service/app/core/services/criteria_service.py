import json
from uuid import UUID

import structlog
from sqlalchemy.ext.asyncio import AsyncSession

from app.api.schemas.criteria import CriteriaSuggestion, CriteriaSuggestRequest
from app.core.services.ai_logging_service import AiLoggingService, logged_ai_call
from app.infrastructure.ai_providers.base import AiProvider

logger = structlog.get_logger()

_SYSTEM_PROMPT = """You are an expert recruitment consultant. Given a job title and \
description, suggest evaluation criteria for screening candidates.

Each criterion must have:
- "name": descriptive name (string)
- "category": one of "Skill", "Experience", "Certification", "Education", "Location", "Custom"
- "evaluation_method": one of "ExactMatch", "RangeMatch", "SemanticSimilarity"
- "is_required": whether this is a hard requirement (boolean)
- "weight": importance weight from 0.00 to 100.00 (decimal). \
All weights should sum to approximately 100.
- "configuration": a JSON string with category-specific config. \
For Skill: {"keywords": [...]}. For Experience: {"min_years": N}. \
For Education: {"degree_level": "..."}. For Certification: {"names": [...]}. \
For Location: {"locations": [...]}. For Custom: {"description": "..."}.

Suggest 5-8 criteria that cover the key requirements from the job description.
Use "SemanticSimilarity" for skills and experience, "ExactMatch" for \
certifications and education, "RangeMatch" for years of experience.

Return a JSON object with a "suggestions" key containing an array of criteria objects.
Return ONLY valid JSON. No markdown, no explanation."""


class CriteriaService:
    def __init__(self, session: AsyncSession, provider: AiProvider, logging_service: AiLoggingService):
        self._session = session
        self._provider = provider
        self._logging_service = logging_service

    async def suggest(self, request: CriteriaSuggestRequest, tenant_id: UUID) -> list[CriteriaSuggestion]:
        user_prompt = f"Job Title: {request.job_title}\n\nJob Description:\n{request.job_description}"

        result = await logged_ai_call(
            provider=self._provider,
            logging_service=self._logging_service,
            tenant_id=tenant_id,
            call_type="CriteriaGeneration",
            system_prompt=_SYSTEM_PROMPT,
            user_prompt=user_prompt,
            request_summary={"job_title": request.job_title},
        )

        data: dict = json.loads(result.content)
        suggestions = [CriteriaSuggestion.model_validate(s) for s in data.get("suggestions", [])]

        await self._session.commit()
        return suggestions
