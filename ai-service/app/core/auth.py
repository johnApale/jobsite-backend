from uuid import UUID

from fastapi import Depends, Request
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from jose import JWTError, jwt
from pydantic import BaseModel

from app.core.config import Settings, get_settings
from app.core.errors import AppErrors

_bearer_scheme = HTTPBearer(auto_error=False)


class JwtClaims(BaseModel):
    sub: UUID
    tenant_id: UUID
    role: str
    email: str


def decode_jwt(token: str, settings: Settings) -> JwtClaims:
    try:
        payload: dict = jwt.decode(token, settings.jwt_secret, algorithms=[settings.jwt_algorithm])
    except JWTError:
        raise AppErrors.unauthorized("Invalid or expired token")

    sub = payload.get("sub")
    tenant_id = payload.get("tenant_id")
    role = payload.get("role")
    email = payload.get("email")

    if not all([sub, tenant_id, role, email]):
        raise AppErrors.unauthorized("Token missing required claims")

    return JwtClaims(sub=UUID(sub), tenant_id=UUID(tenant_id), role=role, email=email)


async def get_current_user(
    request: Request,
    credentials: HTTPAuthorizationCredentials | None = Depends(_bearer_scheme),
    settings: Settings = Depends(get_settings),
) -> JwtClaims:
    if credentials is None:
        raise AppErrors.unauthorized()

    claims = decode_jwt(credentials.credentials, settings)

    structlog_bind = getattr(request.state, "correlation_id", None)
    if structlog_bind:
        import structlog

        structlog.contextvars.bind_contextvars(tenant_id=str(claims.tenant_id), user_id=str(claims.sub))

    return claims
