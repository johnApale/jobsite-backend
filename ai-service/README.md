# AI Service

Standalone Python/FastAPI microservice providing AI-powered capabilities for D'Jobsite iConnect.

## Capabilities

- **Resume Parsing** — Extract structured skills, experience, education from resume text (with SHA-256 cache)
- **Criteria Suggestion** — AI-generated evaluation criteria for job postings
- **Assessment Questions** — AI-suggested screening questions based on job criteria
- **Screening Evaluation** — Per-criterion scoring of applicants with weighted overall score
- **Answer Scoring** — AI evaluation of free-text candidate answers
- **Candidate Feedback** — Transparency-level-aware feedback generation

## Architecture

- **Framework**: FastAPI with async endpoints
- **Database**: Shared PostgreSQL with `ai_service` schema (SQLAlchemy 2.0 async + asyncpg)
- **AI Provider**: OpenAI (abstracted for future multi-provider support)
- **Auth**: JWT (shared HS256 secret with .NET monolith)
- **Logging**: structlog with correlation ID propagation

## Setup

```bash
cd ai-service
python3 -m venv .venv
source .venv/bin/activate
pip install -e ".[dev]"
```

## Configuration

Environment variables (or `.env` file):

| Variable         | Default                                                            | Description             |
| ---------------- | ------------------------------------------------------------------ | ----------------------- |
| `DATABASE_URL`   | `postgresql+asyncpg://postgres:postgres@localhost:5432/jobsite_ai` | PostgreSQL connection   |
| `JWT_SECRET`     | `change-me-in-production`                                          | Shared JWT signing key  |
| `OPENAI_API_KEY` | (empty)                                                            | OpenAI API key          |
| `OPENAI_MODEL`   | `gpt-4o`                                                           | Model for AI calls      |
| `ENABLE_DOCS`    | `false`                                                            | Enable Swagger/ReDoc UI |
| `LOG_LEVEL`      | `INFO`                                                             | Logging level           |
| `CORS_ORIGINS`   | `["http://localhost:3000"]`                                        | Allowed CORS origins    |

## Run

```bash
uvicorn app.main:app --reload --port 8000
```

## Database Migrations

```bash
# Generate migration
alembic revision --autogenerate -m "description"

# Apply migrations
alembic upgrade head
```

## Test

```bash
pytest                    # all tests
pytest tests/ -v          # verbose output
pytest tests/test_api.py  # endpoint tests only
```

## API Endpoints

| Method | Path                                 | Description            |
| ------ | ------------------------------------ | ---------------------- |
| GET    | `/health`                            | Health check (no auth) |
| POST   | `/api/v1/ai/resumes/parse`           | Parse resume text      |
| POST   | `/api/v1/ai/criteria/suggest`        | Suggest criteria       |
| POST   | `/api/v1/ai/assessment/suggest`      | Suggest questions      |
| POST   | `/api/v1/ai/screening/evaluate`      | Evaluate applicant     |
| POST   | `/api/v1/ai/screening/score-answers` | Score answers          |
| POST   | `/api/v1/ai/screening/feedback`      | Generate feedback      |

See [docs/api-reference/ai-service.md](../docs/api-reference/ai-service.md) for full API reference.
