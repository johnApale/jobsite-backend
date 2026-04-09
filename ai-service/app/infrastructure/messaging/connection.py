import structlog
from aio_pika import connect_robust
from aio_pika.abc import AbstractRobustConnection, AbstractRobustChannel

logger = structlog.get_logger()

_connection: AbstractRobustConnection | None = None
_channel: AbstractRobustChannel | None = None


async def connect(rabbitmq_url: str) -> AbstractRobustChannel:
    """Establish a robust connection and channel to RabbitMQ."""
    global _connection, _channel

    _connection = await connect_robust(rabbitmq_url)
    _channel = await _connection.channel()
    await _channel.set_qos(prefetch_count=10)

    await logger.ainfo("RabbitMQ connection established")
    return _channel


async def get_channel() -> AbstractRobustChannel:
    """Return the current channel. Raises if not connected."""
    if _channel is None or _channel.is_closed:
        raise RuntimeError("RabbitMQ channel is not available — call connect() first")
    return _channel


async def disconnect() -> None:
    """Gracefully close the channel and connection."""
    global _connection, _channel

    if _channel and not _channel.is_closed:
        await _channel.close()
        _channel = None

    if _connection and not _connection.is_closed:
        await _connection.close()
        _connection = None

    await logger.ainfo("RabbitMQ connection closed")
