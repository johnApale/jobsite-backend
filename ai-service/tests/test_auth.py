import uuid
from datetime import datetime, timezone

import pytest
from jose import jwt

from app.core.auth import JwtClaims, decode_jwt
from app.core.config import Settings
from app.core.errors import AppError

_TEST_SECRET = "test-secret"


@pytest.fixture
def settings() -> Settings:
    return Settings(
        database_url="postgresql+asyncpg://test:test@localhost/test",
        jwt_secret=_TEST_SECRET,
        openai_api_key="test",
    )


def _make_token(payload: dict) -> str:
    return jwt.encode(payload, _TEST_SECRET, algorithm="HS256")


def test_decode_jwt_valid_token_returns_claims(settings):
    token = _make_token({
        "sub": str(uuid.uuid4()),
        "tenant_id": str(uuid.uuid4()),
        "role": "Recruiter",
        "email": "user@test.com",
        "exp": int(datetime(2030, 1, 1, tzinfo=timezone.utc).timestamp()),
    })

    claims = decode_jwt(token, settings)

    assert isinstance(claims, JwtClaims)
    assert claims.role == "Recruiter"
    assert claims.email == "user@test.com"


def test_decode_jwt_expired_token_raises_unauthorized(settings):
    token = _make_token({
        "sub": str(uuid.uuid4()),
        "tenant_id": str(uuid.uuid4()),
        "role": "Recruiter",
        "email": "user@test.com",
        "exp": int(datetime(2020, 1, 1, tzinfo=timezone.utc).timestamp()),
    })

    with pytest.raises(AppError) as exc_info:
        decode_jwt(token, settings)
    assert exc_info.value.code == "UNAUTHORIZED"


def test_decode_jwt_invalid_signature_raises_unauthorized(settings):
    token = jwt.encode(
        {"sub": str(uuid.uuid4()), "tenant_id": str(uuid.uuid4()), "role": "Recruiter", "email": "u@t.com", "exp": 9999999999},
        "wrong-secret",
        algorithm="HS256",
    )

    with pytest.raises(AppError) as exc_info:
        decode_jwt(token, settings)
    assert exc_info.value.code == "UNAUTHORIZED"


def test_decode_jwt_missing_tenant_id_raises_unauthorized(settings):
    token = _make_token({
        "sub": str(uuid.uuid4()),
        "role": "Recruiter",
        "email": "user@test.com",
        "exp": int(datetime(2030, 1, 1, tzinfo=timezone.utc).timestamp()),
    })

    with pytest.raises(AppError) as exc_info:
        decode_jwt(token, settings)
    assert exc_info.value.code == "UNAUTHORIZED"


def test_decode_jwt_missing_role_raises_unauthorized(settings):
    token = _make_token({
        "sub": str(uuid.uuid4()),
        "tenant_id": str(uuid.uuid4()),
        "email": "user@test.com",
        "exp": int(datetime(2030, 1, 1, tzinfo=timezone.utc).timestamp()),
    })

    with pytest.raises(AppError) as exc_info:
        decode_jwt(token, settings)
    assert exc_info.value.code == "UNAUTHORIZED"
