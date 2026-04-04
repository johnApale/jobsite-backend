import uuid
from datetime import datetime

from sqlalchemy import DateTime, Index, String, UniqueConstraint, text
from sqlalchemy.dialects.postgresql import JSONB, UUID
from sqlalchemy.orm import Mapped, mapped_column

from app.core.models.base import Base


class ParsedResumeCache(Base):
    __tablename__ = "parsed_resume_cache"

    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()")
    )
    file_hash: Mapped[str] = mapped_column(String(64), nullable=False)
    parsed_result: Mapped[dict] = mapped_column(JSONB, nullable=False)
    ai_provider: Mapped[str] = mapped_column(String(20), nullable=False)
    ai_model: Mapped[str] = mapped_column(String(50), nullable=False)
    expires_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True), nullable=False, server_default=text("NOW()")
    )

    __table_args__ = (
        UniqueConstraint("file_hash", name="uq_parsed_resume_cache_hash"),
        Index("ix_parsed_resume_cache_hash", "file_hash", unique=True),
        Index("ix_parsed_resume_cache_expires", "expires_at"),
        {"schema": "ai_service"},
    )
