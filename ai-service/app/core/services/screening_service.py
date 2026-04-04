import json
from decimal import Decimal
from uuid import UUID

import structlog
from sqlalchemy.ext.asyncio import AsyncSession

from app.api.schemas.answer_scoring import AnswerScoreResult, ScoreAnswersRequest, ScoreAnswersResponse
from app.api.schemas.feedback import FeedbackRequest, FeedbackResponse
from app.api.schemas.screening import (
    CriterionScore,
    ScreeningEvaluateRequest,
    ScreeningEvaluateResponse,
)
from app.core.services.ai_logging_service import AiLoggingService, logged_ai_call
from app.infrastructure.ai_providers.base import AiProvider

logger = structlog.get_logger()

_EVALUATE_SYSTEM_PROMPT = """You are an expert candidate evaluator. Given an applicant's resume/profile data and job evaluation criteria, score each criterion.

For each criterion, provide:
- "criterion_id": the UUID of the criterion (string)
- "criterion_name": the name of the criterion
- "category": the criterion category
- "weight": the criterion weight
- "score": a score from 0.00 to 100.00 (decimal)
- "result": one of "Pass", "Fail", or "Required" (if is_required and score < 50, use "Required")
- "reasoning": a brief explanation of why this score was assigned

Also compute:
- "overall_score": weighted average of all criterion scores

Return a JSON object with "breakdown" (array of criterion scores) and "overall_score" (decimal).
Return ONLY valid JSON. No markdown, no explanation."""

_SCORE_ANSWERS_SYSTEM_PROMPT = """You are an expert answer evaluator. Score each candidate answer to a screening question.

For each answer, provide:
- "question_id": the UUID of the question (string)
- "score": a score from 0 to 100 (decimal)
- "result": "Pass" if score >= 50, "Fail" if score < 50
- "reasoning": brief explanation of the score

Evaluate based on:
- Relevance to the question
- Depth and specificity of the answer
- Coverage of key topics (if provided)
- Quality and clarity of communication

Return a JSON object with a "scores" key containing an array of score objects.
Return ONLY valid JSON. No markdown, no explanation."""

_FEEDBACK_SYSTEM_PROMPT = """You are a professional career advisor. Generate candidate-facing feedback based on their screening results.

The feedback should be:
- Constructive and encouraging
- Specific about strengths identified
- Tactful about areas for improvement
- Professional in tone

Transparency levels:
- "Full": Provide detailed feedback covering each criterion, specific scores, and actionable improvement suggestions.
- "Summary": Provide a high-level overview of strengths and areas for improvement without specific scores.
- "None": Provide a brief, generic acknowledgment of the application.

Return a JSON object with a "feedback" key containing the feedback text string.
Return ONLY valid JSON. No markdown, no explanation."""


class ScreeningService:
    def __init__(self, session: AsyncSession, provider: AiProvider, logging_service: AiLoggingService):
        self._session = session
        self._provider = provider
        self._logging_service = logging_service

    async def evaluate(self, request: ScreeningEvaluateRequest, tenant_id: UUID) -> ScreeningEvaluateResponse:
        criteria_text = "\n".join(
            f"- ID: {c.id}, Name: {c.name}, Category: {c.category}, "
            f"Method: {c.evaluation_method}, Required: {c.is_required}, "
            f"Weight: {c.weight}, Config: {c.configuration}"
            for c in request.criteria
        )
        applicant_text = (
            f"Profile Skills: {request.applicant.profile_skills or 'N/A'}\n"
            f"Resume Text: {request.applicant.resume_parsed_text or 'N/A'}\n"
            f"Extracted Skills: {request.applicant.resume_extracted_skills or 'N/A'}\n"
            f"AI Parsed Content: {request.applicant.ai_parsed_content or 'N/A'}"
        )
        user_prompt = f"Evaluation Criteria:\n{criteria_text}\n\nApplicant Data:\n{applicant_text}"

        result = await logged_ai_call(
            provider=self._provider,
            logging_service=self._logging_service,
            tenant_id=tenant_id,
            call_type="ScreeningEvaluation",
            system_prompt=_EVALUATE_SYSTEM_PROMPT,
            user_prompt=user_prompt,
            request_summary={"criteria_count": len(request.criteria)},
        )

        data: dict = json.loads(result.content)
        breakdown = [CriterionScore.model_validate(s) for s in data.get("breakdown", [])]
        overall_score = Decimal(str(data.get("overall_score", 0)))

        await self._session.commit()
        return ScreeningEvaluateResponse(breakdown=breakdown, overall_score=overall_score)

    async def score_answers(self, request: ScoreAnswersRequest, tenant_id: UUID) -> ScoreAnswersResponse:
        answers_text = "\n\n".join(
            f"Question ID: {a.question_id}\n"
            f"Question: {a.question_text}\n"
            f"Answer: {a.response_text}\n"
            f"Scoring Guidance: {a.scoring_guidance or 'N/A'}\n"
            f"Key Topics: {', '.join(a.key_topics) if a.key_topics else 'N/A'}"
            for a in request.answers
        )
        user_prompt = f"Score the following candidate answers:\n\n{answers_text}"

        result = await logged_ai_call(
            provider=self._provider,
            logging_service=self._logging_service,
            tenant_id=tenant_id,
            call_type="AnswerScoring",
            system_prompt=_SCORE_ANSWERS_SYSTEM_PROMPT,
            user_prompt=user_prompt,
            request_summary={"answer_count": len(request.answers)},
        )

        data: dict = json.loads(result.content)
        scores = [AnswerScoreResult.model_validate(s) for s in data.get("scores", [])]

        await self._session.commit()
        return ScoreAnswersResponse(scores=scores)

    async def generate_feedback(self, request: FeedbackRequest, tenant_id: UUID) -> FeedbackResponse:
        user_prompt = (
            f"Criteria Breakdown: {request.criteria_breakdown}\n"
            f"Overall Score: {request.overall_score}\n"
            f"Transparency Level: {request.transparency_level}"
        )

        result = await logged_ai_call(
            provider=self._provider,
            logging_service=self._logging_service,
            tenant_id=tenant_id,
            call_type="FeedbackGeneration",
            system_prompt=_FEEDBACK_SYSTEM_PROMPT,
            user_prompt=user_prompt,
            request_summary={"transparency_level": request.transparency_level},
        )

        data: dict = json.loads(result.content)
        feedback = data.get("feedback", "")

        await self._session.commit()
        return FeedbackResponse(feedback=feedback)
