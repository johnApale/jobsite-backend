from fastapi import APIRouter, Depends
from sqlalchemy.ext.asyncio import AsyncSession

from app.api.schemas.assessment import AssessmentSuggestRequest, QuestionSuggestion
from app.core.auth import JwtClaims, get_current_user
from app.core.config import Settings, get_settings
from app.core.services.ai_logging_service import AiLoggingService
from app.core.services.assessment_service import AssessmentService
from app.infrastructure.ai_providers import get_ai_provider
from app.infrastructure.db.session import get_db

router = APIRouter()


@router.post("/suggest", response_model=list[QuestionSuggestion], status_code=200)
async def suggest_assessment_questions(
    request: AssessmentSuggestRequest,
    claims: JwtClaims = Depends(get_current_user),
    session: AsyncSession = Depends(get_db),
    settings: Settings = Depends(get_settings),
) -> list[QuestionSuggestion]:
    provider = get_ai_provider(settings)
    logging_service = AiLoggingService(session)
    service = AssessmentService(session, provider, logging_service)
    return await service.suggest(request, claims.tenant_id)
