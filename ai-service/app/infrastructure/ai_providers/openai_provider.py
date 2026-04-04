import structlog
from openai import APIConnectionError, APIError, AsyncOpenAI, RateLimitError

from app.core.errors import AppErrors
from app.infrastructure.ai_providers.base import AiCompletionResult, AiProvider

logger = structlog.get_logger()


class OpenAiProvider(AiProvider):
    def __init__(self, api_key: str, model: str):
        self._client = AsyncOpenAI(api_key=api_key)
        self._model = model

    @property
    def provider_name(self) -> str:
        return "OpenAI"

    @property
    def model_name(self) -> str:
        return self._model

    async def complete(
        self,
        system_prompt: str,
        user_prompt: str,
        temperature: float = 0.3,
        max_tokens: int = 4096,
    ) -> AiCompletionResult:
        try:
            response = await self._client.chat.completions.create(
                model=self._model,
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": user_prompt},
                ],
                temperature=temperature,
                max_tokens=max_tokens,
                response_format={"type": "json_object"},
            )
        except RateLimitError:
            await logger.awarning("OpenAI rate limit hit")
            raise AppErrors.ai_provider_error("AI provider rate limit exceeded")
        except APIConnectionError:
            await logger.aerror("OpenAI connection error")
            raise AppErrors.ai_provider_error("Cannot connect to AI provider")
        except APIError as exc:
            await logger.aerror("OpenAI API error", status_code=exc.status_code)
            raise AppErrors.ai_provider_error(f"AI provider error: {exc.message}")

        choice = response.choices[0]
        usage = response.usage

        return AiCompletionResult(
            content=choice.message.content or "",
            input_tokens=usage.prompt_tokens if usage else None,
            output_tokens=usage.completion_tokens if usage else None,
            total_tokens=usage.total_tokens if usage else None,
        )
