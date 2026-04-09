# syntax=docker/dockerfile:1
FROM python:3.12-slim AS base

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup appuser

# Install dependencies
COPY pyproject.toml .
RUN --mount=type=cache,target=/root/.cache/pip \
    pip install .

# Copy application code
COPY app/ app/
COPY migrations/ migrations/
COPY alembic.ini .

USER appuser
EXPOSE 8000
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
