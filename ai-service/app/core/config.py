from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
    )

    # Database
    database_url: str = "postgresql+asyncpg://postgres:postgres@localhost:5432/jobsite_ai"

    # JWT (shared secret with monolith)
    jwt_secret: str = "change-me-in-production"
    jwt_algorithm: str = "HS256"

    # OpenAI
    openai_api_key: str = ""
    openai_model: str = "gpt-4o"

    # CORS
    cors_origins: list[str] = ["http://localhost:3000"]

    # Logging
    log_level: str = "INFO"

    # OpenAPI docs (gated — dev only)
    enable_docs: bool = False


def get_settings() -> Settings:
    return Settings()
