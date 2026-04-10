import uuid
from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from fastapi.testclient import TestClient

import app.main
import app.api.routes.resume
import app.api.routes.criteria
import app.api.routes.assessment
import app.api.routes.screening
from app.core.auth import JwtClaims, get_current_user
from app.core.config import Settings, get_settings
from app.infrastructure.ai_providers.base import AiCompletionResult
from app.infrastructure.db.session import get_db

_TEST_SECRET = "test-api-secret"
_TEST_TENANT_ID = str(uuid.uuid4())
_TEST_USER_ID = str(uuid.uuid4())


@pytest.fixture
def shared_mock_provider() -> MagicMock:
    """Shared mock provider — tests set `complete.return_value` before each request."""
    provider = MagicMock()
    provider.provider_name = "OpenAI"
    provider.model_name = "gpt-4o"
    provider.complete = AsyncMock()
    return provider


@pytest.fixture
def api_client(shared_mock_provider) -> TestClient:
    settings = Settings(
        database_url="postgresql+asyncpg://test:test@localhost/test",
        jwt_secret=_TEST_SECRET,
        openai_api_key="test",
        enable_docs=False,
    )

    mock_session = AsyncMock()
    mock_session.add = MagicMock()
    mock_session.flush = AsyncMock()
    mock_session.commit = AsyncMock()
    mock_session.execute = AsyncMock(return_value=MagicMock(scalar_one_or_none=MagicMock(return_value=None)))

    mock_engine = MagicMock()
    mock_engine.dispose = AsyncMock()

    mock_channel = MagicMock()

    with patch.object(app.main, "get_settings", return_value=settings), \
         patch("app.infrastructure.db.engine.create_engine", return_value=mock_engine), \
         patch("app.infrastructure.db.session.init_session_factory"), \
         patch("app.infrastructure.messaging.connection.connect_robust", new_callable=AsyncMock) as mock_rmq, \
         patch("app.infrastructure.messaging.consumer.register_consumer", new_callable=AsyncMock), \
         patch("app.infrastructure.messaging.connection.disconnect", new_callable=AsyncMock), \
         patch.object(app.api.routes.resume, "get_ai_provider", return_value=shared_mock_provider), \
         patch.object(app.api.routes.criteria, "get_ai_provider", return_value=shared_mock_provider), \
         patch.object(app.api.routes.assessment, "get_ai_provider", return_value=shared_mock_provider), \
         patch.object(app.api.routes.screening, "get_ai_provider", return_value=shared_mock_provider):

        mock_connection = AsyncMock()
        mock_connection.channel = AsyncMock(return_value=mock_channel)
        mock_rmq.return_value = mock_connection
        mock_channel.set_qos = AsyncMock()

        app_instance = app.main.create_app()

        test_claims = JwtClaims(
            sub=uuid.UUID(_TEST_USER_ID),
            tenant_id=uuid.UUID(_TEST_TENANT_ID),
            role="Recruiter",
            email="t@t.com",
        )

        async def override_get_db():
            yield mock_session

        app_instance.dependency_overrides[get_db] = override_get_db
        app_instance.dependency_overrides[get_settings] = lambda: settings
        app_instance.dependency_overrides[get_current_user] = lambda: test_claims

        with TestClient(app_instance) as c:
            yield c

        app_instance.dependency_overrides.clear()


# --- Resume Parsing ---

def test_parse_resume_endpoint_returns_200(api_client, shared_mock_provider):
    shared_mock_provider.complete.return_value = AiCompletionResult(
        content='{"skills": [{"name": "Python"}], "experience": [], "education": [], "certifications": [], "summary": "Dev"}',
        input_tokens=100, output_tokens=50, total_tokens=150,
    )
    response = api_client.post(
        "/api/v1/ai/resumes/parse",
        json={"parsed_text": "I am a Python developer with 5 years experience"},
    )
    assert response.status_code == 200
    body = response.json()
    assert "skills" in body or "summary" in body


def test_parse_resume_endpoint_missing_text_returns_422(api_client, shared_mock_provider):
    response = api_client.post(
        "/api/v1/ai/resumes/parse",
        json={},
    )
    assert response.status_code == 422


# --- Criteria Suggestion ---

def test_suggest_criteria_endpoint_returns_200(api_client, shared_mock_provider):
    shared_mock_provider.complete.return_value = AiCompletionResult(
        content='{"suggestions": [{"name": "Python", "category": "Skill", "evaluation_method": "SemanticSimilarity", "is_required": true, "weight": 50.0, "configuration": "{}"}]}',
        input_tokens=100, output_tokens=50, total_tokens=150,
    )
    response = api_client.post(
        "/api/v1/ai/criteria/suggest",
        json={"job_title": "Python Dev", "job_description": "Need Python expert"},
    )
    assert response.status_code == 200


def test_suggest_criteria_endpoint_missing_title_returns_422(api_client, shared_mock_provider):
    response = api_client.post(
        "/api/v1/ai/criteria/suggest",
        json={"job_description": "Some description"},
    )
    assert response.status_code == 422


# --- Assessment Suggestion ---

def test_suggest_assessment_endpoint_returns_200(api_client, shared_mock_provider):
    shared_mock_provider.complete.return_value = AiCompletionResult(
        content='{"suggestions": [{"question_text": "Describe experience", "question_type": "FreeText", "timing": "AfterScreening", "is_required": true, "weight": 50.0}]}',
        input_tokens=100, output_tokens=50, total_tokens=150,
    )
    response = api_client.post(
        "/api/v1/ai/assessment/suggest",
        json={
            "job_description": "Python developer",
            "criteria": [{"id": str(uuid.uuid4()), "name": "Python", "category": "Skill", "evaluation_method": "SemanticSimilarity", "is_required": True, "weight": 50.0, "configuration": "{}"}],
        },
    )
    assert response.status_code == 200


def test_suggest_assessment_endpoint_empty_criteria_returns_422(api_client, shared_mock_provider):
    response = api_client.post(
        "/api/v1/ai/assessment/suggest",
        json={"job_description": "Dev"},
    )
    assert response.status_code == 422


# --- Screening Evaluation ---

def test_evaluate_endpoint_returns_200(api_client, shared_mock_provider):
    cid = str(uuid.uuid4())
    shared_mock_provider.complete.return_value = AiCompletionResult(
        content=f'{{"breakdown": [{{"criterion_id": "{cid}", "criterion_name": "Python", "category": "Skill", "weight": 50.0, "score": 80.0, "result": "Pass", "reasoning": "Good"}}], "overall_score": 80.0}}',
        input_tokens=200, output_tokens=100, total_tokens=300,
    )
    response = api_client.post(
        "/api/v1/ai/screening/evaluate",
        json={
            "criteria": [{"id": cid, "name": "Python", "category": "Skill", "evaluation_method": "SemanticSimilarity", "is_required": True, "weight": 50.0, "configuration": "{}"}],
            "applicant": {"resume_parsed_text": "Python developer"},
        },
    )
    assert response.status_code == 200


def test_evaluate_endpoint_missing_criteria_returns_422(api_client, shared_mock_provider):
    response = api_client.post(
        "/api/v1/ai/screening/evaluate",
        json={"applicant": {"resume_parsed_text": "test"}},
    )
    assert response.status_code == 422


# --- Answer Scoring ---

def test_score_answers_endpoint_returns_200(api_client, shared_mock_provider):
    qid = str(uuid.uuid4())
    shared_mock_provider.complete.return_value = AiCompletionResult(
        content=f'{{"scores": [{{"question_id": "{qid}", "score": 75.0, "result": "Pass", "reasoning": "Good answer"}}]}}',
        input_tokens=100, output_tokens=50, total_tokens=150,
    )
    response = api_client.post(
        "/api/v1/ai/screening/score-answers",
        json={"answers": [{"question_id": qid, "question_text": "Experience?", "response_text": "5 years"}]},
    )
    assert response.status_code == 200


def test_score_answers_endpoint_empty_answers_returns_422(api_client, shared_mock_provider):
    response = api_client.post(
        "/api/v1/ai/screening/score-answers",
        json={},
    )
    assert response.status_code == 422


# --- Candidate Feedback ---

def test_feedback_endpoint_returns_200(api_client, shared_mock_provider):
    shared_mock_provider.complete.return_value = AiCompletionResult(
        content='{"feedback": "You demonstrated strong technical skills."}',
        input_tokens=100, output_tokens=50, total_tokens=150,
    )
    response = api_client.post(
        "/api/v1/ai/screening/feedback",
        json={"criteria_breakdown": "[{}]", "overall_score": 80.0, "transparency_level": "Full"},
    )
    assert response.status_code == 200
    assert "feedback" in response.json()
