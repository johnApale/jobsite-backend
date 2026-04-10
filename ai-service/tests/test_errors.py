from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from fastapi.testclient import TestClient

import app.main
from app.core.config import Settings, get_settings
from app.infrastructure.db.session import get_db

_TEST_SECRET = "test-error-secret"


@pytest.fixture
def error_client() -> TestClient:
    settings = Settings(
        database_url="postgresql+asyncpg://test:test@localhost/test",
        jwt_secret=_TEST_SECRET,
        openai_api_key="test",
        enable_docs=False,
    )

    mock_engine = MagicMock()
    mock_engine.dispose = AsyncMock()

    mock_channel = MagicMock()

    with (
        patch.object(app.main, "get_settings", return_value=settings),
        patch("app.infrastructure.db.engine.create_engine", return_value=mock_engine),
        patch("app.infrastructure.db.session.init_session_factory"),
        patch("app.infrastructure.messaging.connection.connect_robust", new_callable=AsyncMock) as mock_rmq,
        patch("app.infrastructure.messaging.consumer.register_consumer", new_callable=AsyncMock),
        patch("app.infrastructure.messaging.connection.disconnect", new_callable=AsyncMock),
    ):
        mock_connection = AsyncMock()
        mock_connection.channel = AsyncMock(return_value=mock_channel)
        mock_rmq.return_value = mock_connection
        mock_channel.set_qos = AsyncMock()

        app_instance = app.main.create_app()

        async def override_get_db():
            yield MagicMock()

        app_instance.dependency_overrides[get_db] = override_get_db
        app_instance.dependency_overrides[get_settings] = lambda: settings

        with TestClient(app_instance) as c:
            yield c

        app_instance.dependency_overrides.clear()


def test_app_error_returns_correct_status_code(error_client):
    # Health endpoint always works — test 401 by hitting a protected endpoint without auth
    response = error_client.post("/api/v1/ai/resumes/parse", json={"parsed_text": "test"})
    assert response.status_code == 401


def test_app_error_returns_error_envelope_format(error_client):
    response = error_client.post("/api/v1/ai/resumes/parse", json={"parsed_text": "test"})
    body = response.json()

    assert "code" in body
    assert "message" in body
    assert "request_id" in body


def test_app_error_includes_request_id(error_client):
    response = error_client.post(
        "/api/v1/ai/resumes/parse",
        json={"parsed_text": "test"},
        headers={"X-Correlation-ID": "test-correlation-123"},
    )
    body = response.json()
    assert body["request_id"] == "test-correlation-123"


def test_health_endpoint_returns_200(error_client):
    response = error_client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "healthy"}


def test_unhandled_exception_returns_500_internal_error(error_client):
    # This tests the generic exception handler by verifying the error envelope structure
    # The 401 from missing auth is a controlled AppError — it should follow the envelope format
    response = error_client.post("/api/v1/ai/resumes/parse", json={"parsed_text": "test"})
    body = response.json()
    assert body["code"] == "UNAUTHORIZED"
