import json
import uuid
from datetime import datetime, timezone
from typing import Any

import structlog
from aio_pika import ExchangeType, Message
from aio_pika.abc import AbstractRobustChannel, AbstractRobustExchange, DeliveryMode

logger = structlog.get_logger()


async def publish(
    channel: AbstractRobustChannel,
    message_type: str,
    payload: dict[str, Any],
    correlation_id: str | None = None,
) -> None:
    """Publish a message to a MassTransit-compatible fanout exchange.

    Wraps the payload in MassTransit's envelope format so the .NET
    monolith can deserialize it with ``IConsumer<T>``.

    Args:
        channel: The aio-pika channel.
        message_type: Full MassTransit exchange name,
            e.g. ``Jobsite.SharedKernel.Events:ResumeParsed``.
        payload: The message body (snake_case dict).
        correlation_id: Optional correlation ID for distributed tracing.
    """
    exchange: AbstractRobustExchange = await channel.declare_exchange(
        message_type,
        ExchangeType.FANOUT,
        durable=True,
    )

    envelope: dict[str, Any] = {
        "messageId": str(uuid.uuid4()),
        "conversationId": str(uuid.uuid4()),
        "correlationId": correlation_id or str(uuid.uuid4()),
        "messageType": [f"urn:message:{message_type.replace(':', '.')}"],
        "message": payload,
        "sentTime": datetime.now(timezone.utc).isoformat(),
        "host": {
            "machineName": "ai-service",
            "processName": "ai-service",
        },
    }

    message = Message(
        body=json.dumps(envelope).encode(),
        content_type="application/json",
        delivery_mode=DeliveryMode.PERSISTENT,
        message_id=envelope["messageId"],
        correlation_id=envelope["correlationId"],
    )

    await exchange.publish(message, routing_key="")

    await logger.ainfo(
        "Message published",
        exchange=message_type,
        message_id=envelope["messageId"],
        correlation_id=envelope["correlationId"],
    )
