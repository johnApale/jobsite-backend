import structlog
from contextlib import asynccontextmanager
from collections.abc import AsyncGenerator

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.core.config import Settings, get_settings
from app.core.errors import AppError, app_error_handler, unhandled_error_handler
from app.core.middleware import CorrelationIdMiddleware
from app.api.routes import resume, criteria, assessment, screening

structlog.configure(
    processors=[
        structlog.contextvars.merge_contextvars,
        structlog.processors.add_log_level,
        structlog.processors.TimeStamper(fmt="iso"),
        structlog.dev.ConsoleRenderer(),
    ],
    wrapper_class=structlog.make_filtering_bound_logger(0),
    context_class=dict,
    logger_factory=structlog.PrintLoggerFactory(),
    cache_logger_on_first_use=True,
)

logger = structlog.get_logger()


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncGenerator[None, None]:
    settings: Settings = get_settings()

    from app.infrastructure.db.engine import create_engine
    from app.infrastructure.db.session import init_session_factory
    from app.infrastructure.messaging.connection import connect, disconnect

    engine = create_engine(settings)
    init_session_factory(engine)
    await logger.ainfo("Database engine initialized")

    if not settings.openai_api_key:
        await logger.awarning("OPENAI_API_KEY is not set — AI endpoints will fail")

    channel = await connect(settings.rabbitmq_url)
    await logger.ainfo("Message broker connected")

    # Consumer registration will be added in Phase 3 when handlers are built
    # e.g. await register_consumer(channel, "Jobsite.SharedKernel.Events:ResumeParseRequested", handler)

    yield

    await disconnect()
    await logger.ainfo("Message broker disconnected")

    await engine.dispose()
    await logger.ainfo("Database engine disposed")


def create_app() -> FastAPI:
    settings: Settings = get_settings()

    app = FastAPI(
        title="Jobsite AI Service",
        lifespan=lifespan,
        docs_url="/docs" if settings.enable_docs else None,
        redoc_url="/redoc" if settings.enable_docs else None,
        openapi_url="/openapi.json" if settings.enable_docs else None,
    )

    app.add_exception_handler(AppError, app_error_handler)
    app.add_exception_handler(Exception, unhandled_error_handler)

    app.add_middleware(CorrelationIdMiddleware)
    app.add_middleware(
        CORSMiddleware,
        allow_origins=settings.cors_origins,
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    app.include_router(resume.router, prefix="/api/v1/ai/resumes", tags=["Resume Parsing"])
    app.include_router(criteria.router, prefix="/api/v1/ai/criteria", tags=["Criteria Suggestion"])
    app.include_router(assessment.router, prefix="/api/v1/ai/assessment", tags=["Assessment Questions"])
    app.include_router(screening.router, prefix="/api/v1/ai/screening", tags=["Screening"])

    @app.get("/health", tags=["Health"])
    async def health():
        return {"status": "healthy"}

    return app


app = create_app()
