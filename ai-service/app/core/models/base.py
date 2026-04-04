from sqlalchemy.orm import DeclarativeBase, MappedAsDataclass


class Base(DeclarativeBase):
    __table_args__ = {"schema": "ai_service"}
