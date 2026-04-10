import json
from collections.abc import Awaitable, Callable
from typing import Any

import structlog
from aio_pika import ExchangeType
from aio_pika.abc import (
    AbstractIncomingMessage,
    AbstractRobustChannel,
    AbstractRobustExchange,
    AbstractRobustQueue,
)

logger = structlog.get_logger()

# MassTransit wraps payloads in an envelope with a "message" key.
# The actual payload is nested under "message".
_MASSTRANSIT_MESSAGE_KEY = "message"

# Consumer service name used in queue naming: <exchange>__<service>
_SERVICE_NAME = "ai-service"

MessageHandler = Callable[[dict[str, Any]], Awaitable[None]]


async def register_consumer(
    channel: AbstractRobustChannel,
    message_type: str,
    handler: MessageHandler,
) -> None:
    """Bind a handler to a MassTransit-compatible exchange.

    MassTransit publishes to a fanout exchange named by the full CLR type,
    e.g. ``Jobsite.SharedKernel.Events:ResumeParseRequested``.

    This function:
    1. Declares a matching fanout exchange (idempotent).
    2. Declares a durable queue named ``<message_type>__<service>``.
    3. Binds the queue to the exchange.
    4. Starts consuming with the provided async handler.
    """
    exchange: AbstractRobustExchange = await channel.declare_exchange(
        message_type,
        ExchangeType.FANOUT,
        durable=True,
    )

    queue_name: str = f"{message_type}__{_SERVICE_NAME}"
    queue: AbstractRobustQueue = await channel.declare_queue(
        queue_name,
        durable=True,
    )
    await queue.bind(exchange)

    async def _on_message(message: AbstractIncomingMessage) -> None:
        async with message.process():
            body: dict[str, Any] = json.loads(message.body)

            # MassTransit envelope: extract nested "message" payload
            payload: dict[str, Any] = body.get(_MASSTRANSIT_MESSAGE_KEY, body)

            await logger.ainfo(
                "Message received",
                exchange=message_type,
                queue=queue_name,
                correlation_id=payload.get("correlation_id"),
            )

            try:
                await handler(payload)
            except Exception:
                await logger.aexception(
                    "Message handler failed",
                    exchange=message_type,
                    correlation_id=payload.get("correlation_id"),
                )
                raise

    await queue.consume(_on_message)
    await logger.ainfo("Consumer registered", exchange=message_type, queue=queue_name)
