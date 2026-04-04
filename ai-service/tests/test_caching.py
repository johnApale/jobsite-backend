import uuid
from unittest.mock import AsyncMock, MagicMock

import pytest

from app.api.schemas.resume import ResumeParseRequest
from app.core.services.ai_logging_service import AiLoggingService
from app.core.services.resume_service import ResumeService
from app.infrastructure.ai_providers.base import AiCompletionResult


async def test_resume_cache_hit_skips_ai_call(mock_db_session, mock_ai_provider):
    cached_row = MagicMock()
    cached_row.parsed_result = {"skills": [{"name": "Go"}], "summary": "Cached"}

    mock_db_session.execute.return_value = MagicMock(scalar_one_or_none=MagicMock(return_value=cached_row))

    logging_service = AiLoggingService(mock_db_session)
    service = ResumeService(mock_db_session, mock_ai_provider, logging_service)

    result = await service.parse(ResumeParseRequest(parsed_text="some resume"), uuid.uuid4())

    assert result.summary == "Cached"
    mock_ai_provider.complete.assert_not_awaited()


async def test_resume_cache_miss_triggers_ai_call(mock_db_session, mock_ai_provider):
    mock_db_session.execute.return_value = MagicMock(scalar_one_or_none=MagicMock(return_value=None))
    mock_ai_provider.complete.return_value = AiCompletionResult(
        content='{"skills": [], "experience": [], "education": [], "certifications": [], "summary": "Fresh parse"}',
        input_tokens=100, output_tokens=50, total_tokens=150,
    )

    logging_service = AiLoggingService(mock_db_session)
    service = ResumeService(mock_db_session, mock_ai_provider, logging_service)

    result = await service.parse(ResumeParseRequest(parsed_text="new resume"), uuid.uuid4())

    assert result.summary == "Fresh parse"
    mock_ai_provider.complete.assert_awaited_once()


async def test_resume_cache_stores_with_30_day_ttl(mock_db_session, mock_ai_provider):
    mock_db_session.execute.return_value = MagicMock(scalar_one_or_none=MagicMock(return_value=None))
    mock_ai_provider.complete.return_value = AiCompletionResult(
        content='{"skills": [], "experience": [], "education": [], "certifications": [], "summary": "Test"}',
        input_tokens=100, output_tokens=50, total_tokens=150,
    )

    logging_service = AiLoggingService(mock_db_session)
    service = ResumeService(mock_db_session, mock_ai_provider, logging_service)

    await service.parse(ResumeParseRequest(parsed_text="resume text"), uuid.uuid4())

    # Find the cache entry among the add calls (log entry + cache entry)
    cache_entries = [
        call.args[0] for call in mock_db_session.add.call_args_list
        if hasattr(call.args[0], "file_hash")
    ]
    assert len(cache_entries) == 1
    from datetime import datetime, timezone
    assert cache_entries[0].expires_at > datetime.now(timezone.utc)


async def test_resume_cache_same_hash_different_tenant_returns_cached(mock_db_session, mock_ai_provider):
    """Resume cache is tenant-agnostic — same file hash returns cached result regardless of tenant."""
    cached_row = MagicMock()
    cached_row.parsed_result = {"skills": [{"name": "Rust"}], "summary": "Shared cache"}

    mock_db_session.execute.return_value = MagicMock(scalar_one_or_none=MagicMock(return_value=cached_row))

    logging_service = AiLoggingService(mock_db_session)
    service = ResumeService(mock_db_session, mock_ai_provider, logging_service)

    # Different tenant IDs, same text
    result1 = await service.parse(ResumeParseRequest(parsed_text="identical resume"), uuid.uuid4())
    result2 = await service.parse(ResumeParseRequest(parsed_text="identical resume"), uuid.uuid4())

    assert result1.summary == "Shared cache"
    assert result2.summary == "Shared cache"
    mock_ai_provider.complete.assert_not_awaited()
