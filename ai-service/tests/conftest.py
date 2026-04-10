import uuid
from datetime import UTC, datetime
from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from fastapi.testclient import TestClient
from jose import jwt

from app.core.config import Settings
from app.infrastructure.ai_providers.base import AiCompletionResult, AiProvider

_TEST_JWT_SECRET = "test-secret-key-for-unit-tests"
_TEST_TENANT_ID = str(uuid.uuid4())
_TEST_USER_ID = str(uuid.uuid4())


@pytest.fixture
def test_settings() -> Settings:
    return Settings(
        database_url="postgresql+asyncpg://test:test@localhost:5432/test",
        jwt_secret=_TEST_JWT_SECRET,
        openai_api_key="test-key",
        openai_model="gpt-4o",
        enable_docs=False,
    )


@pytest.fixture
def valid_jwt_token() -> str:
    payload = {
        "sub": _TEST_USER_ID,
        "tenant_id": _TEST_TENANT_ID,
        "role": "Recruiter",
        "email": "test@example.com",
        "exp": int(datetime(2030, 1, 1, tzinfo=UTC).timestamp()),
    }
    return jwt.encode(payload, _TEST_JWT_SECRET, algorithm="HS256")


@pytest.fixture
def expired_jwt_token() -> str:
    payload = {
        "sub": _TEST_USER_ID,
        "tenant_id": _TEST_TENANT_ID,
        "role": "Recruiter",
        "email": "test@example.com",
        "exp": int(datetime(2020, 1, 1, tzinfo=UTC).timestamp()),
    }
    return jwt.encode(payload, _TEST_JWT_SECRET, algorithm="HS256")


@pytest.fixture
def mock_ai_provider() -> MagicMock:
    provider = MagicMock(spec=AiProvider)
    provider.provider_name = "OpenAI"
    provider.model_name = "gpt-4o"
    provider.complete = AsyncMock()
    return provider


@pytest.fixture
def mock_db_session() -> AsyncMock:
    session = AsyncMock()
    session.add = MagicMock()
    session.flush = AsyncMock()
    session.commit = AsyncMock()
    session.execute = AsyncMock()
    return session


@pytest.fixture
def sample_ai_result() -> AiCompletionResult:
    return AiCompletionResult(
        content='{"skills": [], "experience": [], "education": [], "certifications": [], "summary": "Test"}',
        input_tokens=100,
        output_tokens=50,
        total_tokens=150,
    )


def _create_test_client(settings: Settings, mock_session: AsyncMock):
    """Create a TestClient with mocked DB engine/session."""
    import app.main

    mock_engine = MagicMock()
    mock_engine.dispose = AsyncMock()

    with (
        patch.object(app.main, "get_settings", return_value=settings),
        patch("app.infrastructure.db.engine.create_engine", return_value=mock_engine),
        patch("app.infrastructure.db.session.init_session_factory"),
    ):
        app_instance = app.main.create_app()

        from app.core.config import get_settings as get_settings_dep
        from app.infrastructure.db.session import get_db

        async def override_get_db():
            yield mock_session

        app_instance.dependency_overrides[get_db] = override_get_db
        app_instance.dependency_overrides[get_settings_dep] = lambda: settings

        client = TestClient(app_instance)
        return client, app_instance


@pytest.fixture
def client(test_settings, mock_db_session) -> TestClient:
    tc, app_instance = _create_test_client(test_settings, mock_db_session)
    with tc as c:
        yield c
    app_instance.dependency_overrides.clear()
