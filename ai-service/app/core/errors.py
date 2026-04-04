from fastapi import Request
from fastapi.responses import JSONResponse


class AppError(Exception):
    def __init__(
        self,
        code: str,
        status_code: int,
        message: str,
        details: dict | None = None,
    ):
        self.code = code
        self.status_code = status_code
        self.message = message
        self.details = details
        super().__init__(message)


class AppErrors:
    """Predefined sentinel errors matching the monolith's error envelope."""

    @staticmethod
    def validation(details: dict | None = None) -> AppError:
        return AppError(
            code="VALIDATION_ERROR",
            status_code=400,
            message="Request validation failed",
            details=details,
        )

    @staticmethod
    def unauthorized(message: str = "Missing or invalid authentication") -> AppError:
        return AppError(code="UNAUTHORIZED", status_code=401, message=message)

    @staticmethod
    def forbidden(message: str = "Insufficient permissions") -> AppError:
        return AppError(code="FORBIDDEN", status_code=403, message=message)

    @staticmethod
    def internal(message: str = "An unexpected error occurred") -> AppError:
        return AppError(code="INTERNAL_ERROR", status_code=500, message=message)

    @staticmethod
    def service_unavailable(message: str = "Service temporarily unavailable") -> AppError:
        return AppError(code="SERVICE_UNAVAILABLE", status_code=503, message=message)

    @staticmethod
    def ai_provider_error(message: str = "AI provider request failed") -> AppError:
        return AppError(code="AI_PROVIDER_ERROR", status_code=502, message=message)


async def app_error_handler(request: Request, exc: AppError) -> JSONResponse:
    body: dict = {
        "code": exc.code,
        "message": exc.message,
        "request_id": getattr(request.state, "correlation_id", "unknown"),
    }
    if exc.details:
        body["details"] = exc.details
    return JSONResponse(status_code=exc.status_code, content=body)


async def unhandled_error_handler(request: Request, exc: Exception) -> JSONResponse:
    body: dict = {
        "code": "INTERNAL_ERROR",
        "message": "An unexpected error occurred",
        "request_id": getattr(request.state, "correlation_id", "unknown"),
    }
    return JSONResponse(status_code=500, content=body)
