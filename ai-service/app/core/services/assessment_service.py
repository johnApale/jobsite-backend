import json
from uuid import UUID

import structlog
from sqlalchemy.ext.asyncio import AsyncSession

from app.api.schemas.assessment import AssessmentSuggestRequest, QuestionSuggestion
from app.core.services.ai_logging_service import AiLoggingService, logged_ai_call
from app.infrastructure.ai_providers.base import AiProvider

logger = structlog.get_logger()

_SYSTEM_PROMPT = """You are an expert recruitment consultant. Given a job description and \
evaluation criteria, suggest screening questions for candidates.

Each question must have:
- "question_text": the screening question (string)
- "question_type": one of "FreeText", "MultipleChoice", "YesNo"
- "timing": one of "AtApplication", "AfterScreening"
- "is_required": whether the question must be answered (boolean)
- "weight": importance weight from 0.00 to 100.00 (decimal)
- "expected_answer": for YesNo, the expected answer as JSON string \
(e.g., '{"value": true}'). For FreeText, scoring rubric as JSON string \
(e.g., '{"key_topics": ["topic1"], "min_quality": "moderate"}'). \
For MultipleChoice, the correct option index as JSON string \
(e.g., '{"correct_index": 0}').
- "options": ONLY for MultipleChoice — a JSON array string of option objects \
(e.g., '[{"text": "Option A"}, {"text": "Option B"}]'). null for other types.

Suggest 3-5 questions. Mix question types. Focus on AfterScreening timing.
Questions should assess areas that automated criteria scoring cannot easily evaluate.

Return a JSON object with a "suggestions" key containing an array of question objects.
Return ONLY valid JSON. No markdown, no explanation."""


class AssessmentService:
    def __init__(self, session: AsyncSession, provider: AiProvider, logging_service: AiLoggingService):
        self._session = session
        self._provider = provider
        self._logging_service = logging_service

    async def suggest(self, request: AssessmentSuggestRequest, tenant_id: UUID) -> list[QuestionSuggestion]:
        criteria_summary = "\n".join(
            f"- {c.name} ({c.category}, {c.evaluation_method}, weight: {c.weight})" for c in request.criteria
        )
        user_prompt = f"Job Description:\n{request.job_description}\n\nEvaluation Criteria:\n{criteria_summary}"

        result = await logged_ai_call(
            provider=self._provider,
            logging_service=self._logging_service,
            tenant_id=tenant_id,
            call_type="AssessmentQuestionGeneration",
            system_prompt=_SYSTEM_PROMPT,
            user_prompt=user_prompt,
            request_summary={"criteria_count": len(request.criteria)},
        )

        data: dict = json.loads(result.content)
        suggestions = [QuestionSuggestion.model_validate(s) for s in data.get("suggestions", [])]

        await self._session.commit()
        return suggestions
