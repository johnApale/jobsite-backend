from fastapi import APIRouter, Depends
from sqlalchemy.ext.asyncio import AsyncSession

from app.api.schemas.resume import ResumeParseRequest, ResumeParseResponse
from app.core.auth import JwtClaims, get_current_user
from app.core.config import Settings, get_settings
from app.core.services.ai_logging_service import AiLoggingService
from app.core.services.resume_service import ResumeService
from app.infrastructure.ai_providers import get_ai_provider
from app.infrastructure.db.session import get_db

router = APIRouter()


@router.post("/parse", response_model=ResumeParseResponse, status_code=200)
async def parse_resume(
    request: ResumeParseRequest,
    claims: JwtClaims = Depends(get_current_user),
    session: AsyncSession = Depends(get_db),
    settings: Settings = Depends(get_settings),
) -> ResumeParseResponse:
    provider = get_ai_provider(settings)
    logging_service = AiLoggingService(session)
    service = ResumeService(session, provider, logging_service)
    return await service.parse(request, claims.tenant_id)
