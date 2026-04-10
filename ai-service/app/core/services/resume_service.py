import hashlib
import json
from datetime import UTC, datetime, timedelta
from uuid import UUID

import structlog
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.api.schemas.resume import (
    Education,
    Experience,
    ExtractedSkill,
    ResumeParseRequest,
    ResumeParseResponse,
)
from app.core.models.parsed_resume_cache import ParsedResumeCache
from app.core.services.ai_logging_service import AiLoggingService, logged_ai_call
from app.infrastructure.ai_providers.base import AiProvider

logger = structlog.get_logger()

_SYSTEM_PROMPT = """You are an expert resume parser. Extract structured data from the \
provided resume text.

Return a JSON object with these fields:
- "skills": array of objects with "name" (string), "level" (string, one of \
"Beginner", "Intermediate", "Advanced", "Expert" or null), "years" (integer or null)
- "experience": array of objects with "title" (string), "company" (string), \
"start_date" (string or null), "end_date" (string or null), "description" (string or null)
- "education": array of objects with "degree" (string), "institution" (string), \
"start_date" (string or null), "end_date" (string or null), "field" (string or null)
- "certifications": array of strings
- "summary": a brief professional summary string

If a section is not present in the resume, use an empty array or null.
Return ONLY valid JSON. No markdown, no explanation."""

_CACHE_TTL_DAYS = 30


class ResumeService:
    def __init__(self, session: AsyncSession, provider: AiProvider, logging_service: AiLoggingService):
        self._session = session
        self._provider = provider
        self._logging_service = logging_service

    async def parse(self, request: ResumeParseRequest, tenant_id: UUID) -> ResumeParseResponse:
        file_hash = hashlib.sha256(request.parsed_text.encode("utf-8")).hexdigest()

        cached = await self._get_cached(file_hash)
        if cached is not None:
            await logger.ainfo("Resume cache hit", file_hash=file_hash[:12])
            return cached

        result = await logged_ai_call(
            provider=self._provider,
            logging_service=self._logging_service,
            tenant_id=tenant_id,
            call_type="ResumeParsing",
            system_prompt=_SYSTEM_PROMPT,
            user_prompt=request.parsed_text,
            request_summary={"resume_length": len(request.parsed_text)},
        )

        parsed = self._parse_response(result.content)

        await self._store_cache(file_hash, parsed)
        await self._session.commit()

        return parsed

    async def _get_cached(self, file_hash: str) -> ResumeParseResponse | None:
        stmt = select(ParsedResumeCache).where(
            ParsedResumeCache.file_hash == file_hash,
            ParsedResumeCache.expires_at > datetime.now(UTC),
        )
        row = (await self._session.execute(stmt)).scalar_one_or_none()
        if row is None:
            return None
        return ResumeParseResponse.model_validate(row.parsed_result)

    async def _store_cache(self, file_hash: str, parsed: ResumeParseResponse) -> None:
        entry = ParsedResumeCache(
            file_hash=file_hash,
            parsed_result=parsed.model_dump(exclude_none=True),
            ai_provider=self._provider.provider_name,
            ai_model=self._provider.model_name,
            expires_at=datetime.now(UTC) + timedelta(days=_CACHE_TTL_DAYS),
        )
        self._session.add(entry)

    @staticmethod
    def _parse_response(content: str) -> ResumeParseResponse:
        data: dict = json.loads(content)

        skills = [ExtractedSkill.model_validate(s) for s in data.get("skills", [])] or None
        experience = [Experience.model_validate(e) for e in data.get("experience", [])] or None
        education = [Education.model_validate(e) for e in data.get("education", [])] or None
        certifications = data.get("certifications") or None
        summary = data.get("summary")

        return ResumeParseResponse(
            skills=skills,
            experience=experience,
            education=education,
            certifications=certifications,
            summary=summary,
        )
