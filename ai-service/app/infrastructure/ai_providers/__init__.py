from app.core.config import Settings
from app.infrastructure.ai_providers.base import AiCompletionResult, AiProvider
from app.infrastructure.ai_providers.openai_provider import OpenAiProvider


def get_ai_provider(settings: Settings) -> AiProvider:
    return OpenAiProvider(api_key=settings.openai_api_key, model=settings.openai_model)


__all__ = ["AiCompletionResult", "AiProvider", "get_ai_provider"]
