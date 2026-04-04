from fastapi import APIRouter, Depends
from sqlalchemy.ext.asyncio import AsyncSession

from app.api.schemas.answer_scoring import ScoreAnswersRequest, ScoreAnswersResponse
from app.api.schemas.feedback import FeedbackRequest, FeedbackResponse
from app.api.schemas.screening import ScreeningEvaluateRequest, ScreeningEvaluateResponse
from app.core.auth import JwtClaims, get_current_user
from app.core.config import Settings, get_settings
from app.core.services.ai_logging_service import AiLoggingService
from app.core.services.screening_service import ScreeningService
from app.infrastructure.ai_providers import get_ai_provider
from app.infrastructure.db.session import get_db

router = APIRouter()


@router.post("/evaluate", response_model=ScreeningEvaluateResponse, status_code=200)
async def evaluate_screening(
    request: ScreeningEvaluateRequest,
    claims: JwtClaims = Depends(get_current_user),
    session: AsyncSession = Depends(get_db),
    settings: Settings = Depends(get_settings),
) -> ScreeningEvaluateResponse:
    provider = get_ai_provider(settings)
    logging_service = AiLoggingService(session)
    service = ScreeningService(session, provider, logging_service)
    return await service.evaluate(request, claims.tenant_id)


@router.post("/score-answers", response_model=ScoreAnswersResponse, status_code=200)
async def score_answers(
    request: ScoreAnswersRequest,
    claims: JwtClaims = Depends(get_current_user),
    session: AsyncSession = Depends(get_db),
    settings: Settings = Depends(get_settings),
) -> ScoreAnswersResponse:
    provider = get_ai_provider(settings)
    logging_service = AiLoggingService(session)
    service = ScreeningService(session, provider, logging_service)
    return await service.score_answers(request, claims.tenant_id)


@router.post("/feedback", response_model=FeedbackResponse, status_code=200)
async def generate_feedback(
    request: FeedbackRequest,
    claims: JwtClaims = Depends(get_current_user),
    session: AsyncSession = Depends(get_db),
    settings: Settings = Depends(get_settings),
) -> FeedbackResponse:
    provider = get_ai_provider(settings)
    logging_service = AiLoggingService(session)
    service = ScreeningService(session, provider, logging_service)
    return await service.generate_feedback(request, claims.tenant_id)
