from collections.abc import AsyncGenerator

from sqlalchemy.ext.asyncio import AsyncEngine, AsyncSession, async_sessionmaker

_session_factory: async_sessionmaker[AsyncSession] | None = None


def init_session_factory(engine: AsyncEngine) -> None:
    global _session_factory
    _session_factory = async_sessionmaker(engine, expire_on_commit=False)


def get_session_factory() -> async_sessionmaker[AsyncSession]:
    """Return the session factory for use outside FastAPI dependency injection."""
    if _session_factory is None:
        raise RuntimeError("Session factory not initialized. Call init_session_factory first.")
    return _session_factory


async def get_db() -> AsyncGenerator[AsyncSession, None]:
    if _session_factory is None:
        raise RuntimeError("Session factory not initialized. Call init_session_factory first.")
    async with _session_factory() as session:
        yield session
