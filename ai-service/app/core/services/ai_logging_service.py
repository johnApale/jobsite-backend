import time
from datetime import UTC, datetime
from decimal import Decimal
from uuid import UUID

import structlog
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.models.ai_api_log import AiApiLog
from app.infrastructure.ai_providers.base import AiCompletionResult, AiProvider

logger = structlog.get_logger()

# Approximate pricing per 1M tokens (input / output) — update as pricing changes
_MODEL_PRICING: dict[str, tuple[Decimal, Decimal]] = {
    "gpt-4o": (Decimal("2.50"), Decimal("10.00")),
    "gpt-4o-mini": (Decimal("0.15"), Decimal("0.60")),
    "gpt-4-turbo": (Decimal("10.00"), Decimal("30.00")),
}


def _estimate_cost(model: str, input_tokens: int | None, output_tokens: int | None) -> Decimal | None:
    if input_tokens is None or output_tokens is None:
        return None
    pricing = _MODEL_PRICING.get(model)
    if pricing is None:
        return None
    input_price, output_price = pricing
    cost = (Decimal(input_tokens) * input_price + Decimal(output_tokens) * output_price) / Decimal("1000000")
    return cost.quantize(Decimal("0.000001"))


class AiLoggingService:
    def __init__(self, session: AsyncSession):
        self._session = session

    async def log_call(
        self,
        tenant_id: UUID,
        call_type: str,
        ai_provider: AiProvider,
        latency_ms: int,
        http_status_code: int,
        is_success: bool,
        result: AiCompletionResult | None = None,
        reference_id: UUID | None = None,
        error_message: str | None = None,
        retry_count: int = 0,
        request_summary: dict | None = None,
        response_summary: dict | None = None,
    ) -> None:
        estimated_cost = None
        input_tokens = None
        output_tokens = None
        total_tokens = None

        if result is not None:
            input_tokens = result.input_tokens
            output_tokens = result.output_tokens
            total_tokens = result.total_tokens
            estimated_cost = _estimate_cost(ai_provider.model_name, input_tokens, output_tokens)

        log_entry = AiApiLog(
            tenant_id=tenant_id,
            call_type=call_type,
            reference_id=reference_id,
            ai_provider=ai_provider.provider_name,
            ai_model=ai_provider.model_name,
            input_tokens=input_tokens,
            output_tokens=output_tokens,
            total_tokens=total_tokens,
            estimated_cost_usd=estimated_cost,
            latency_ms=latency_ms,
            http_status_code=http_status_code,
            is_success=is_success,
            error_message=error_message[:1000] if error_message else None,
            retry_count=retry_count,
            request_summary=request_summary,
            response_summary=response_summary,
            called_at=datetime.now(UTC),
        )

        self._session.add(log_entry)
        await self._session.flush()


async def logged_ai_call(
    provider: AiProvider,
    logging_service: AiLoggingService,
    tenant_id: UUID,
    call_type: str,
    system_prompt: str,
    user_prompt: str,
    reference_id: UUID | None = None,
    request_summary: dict | None = None,
    temperature: float = 0.3,
    max_tokens: int = 4096,
) -> AiCompletionResult:
    """Execute an AI provider call with automatic logging."""
    start = time.monotonic()
    try:
        result = await provider.complete(system_prompt, user_prompt, temperature=temperature, max_tokens=max_tokens)
        latency_ms = int((time.monotonic() - start) * 1000)

        await logging_service.log_call(
            tenant_id=tenant_id,
            call_type=call_type,
            ai_provider=provider,
            latency_ms=latency_ms,
            http_status_code=200,
            is_success=True,
            result=result,
            reference_id=reference_id,
            request_summary=request_summary,
        )
        return result

    except Exception as exc:
        latency_ms = int((time.monotonic() - start) * 1000)
        await logging_service.log_call(
            tenant_id=tenant_id,
            call_type=call_type,
            ai_provider=provider,
            latency_ms=latency_ms,
            http_status_code=502,
            is_success=False,
            reference_id=reference_id,
            error_message=str(exc),
            request_summary=request_summary,
        )
        raise
