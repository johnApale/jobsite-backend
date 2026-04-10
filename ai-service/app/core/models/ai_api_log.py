import uuid
from datetime import datetime

from sqlalchemy import Boolean, DateTime, Index, Integer, Numeric, String, text
from sqlalchemy.dialects.postgresql import JSONB, UUID
from sqlalchemy.orm import Mapped, mapped_column

from app.core.models.base import Base


class AiApiLog(Base):
    __tablename__ = "ai_api_logs"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()")
    )
    tenant_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), nullable=False)
    call_type: Mapped[str] = mapped_column(String(30), nullable=False)
    reference_id: Mapped[uuid.UUID | None] = mapped_column(UUID(as_uuid=True), nullable=True)
    ai_provider: Mapped[str] = mapped_column(String(20), nullable=False)
    ai_model: Mapped[str] = mapped_column(String(50), nullable=False)
    input_tokens: Mapped[int | None] = mapped_column(Integer, nullable=True)
    output_tokens: Mapped[int | None] = mapped_column(Integer, nullable=True)
    total_tokens: Mapped[int | None] = mapped_column(Integer, nullable=True)
    estimated_cost_usd: Mapped[float | None] = mapped_column(Numeric(10, 6), nullable=True)
    latency_ms: Mapped[int] = mapped_column(Integer, nullable=False)
    http_status_code: Mapped[int] = mapped_column(Integer, nullable=False)
    is_success: Mapped[bool] = mapped_column(Boolean, nullable=False)
    error_message: Mapped[str | None] = mapped_column(String(1000), nullable=True)
    retry_count: Mapped[int] = mapped_column(Integer, nullable=False, server_default=text("0"))
    request_summary: Mapped[dict | None] = mapped_column(JSONB, nullable=True)
    response_summary: Mapped[dict | None] = mapped_column(JSONB, nullable=True)
    called_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False, server_default=text("NOW()"))

    __table_args__ = (
        Index("ix_api_logs_tenant_id", "tenant_id"),
        Index("ix_api_logs_call_type", "call_type"),
        Index("ix_api_logs_called_at", "called_at"),
        Index("ix_api_logs_provider_model", "ai_provider", "ai_model"),
        Index("ix_api_logs_is_success", "is_success"),
        {"schema": "ai_service"},
    )
