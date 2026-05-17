from uuid import uuid4
from beanie import Document
from pydantic import Field


class DocumentBase(Document):
    """
    Base class for all MongoDB documents.

    `id` is a string UUID (same pattern as the .NET project).
    `ver` is kept for future schema migration tracking.
    """

    id: str = Field(default_factory=lambda: str(uuid4()))
    ver: int = 1

    class Settings:
        # Subclasses must override `name` with their collection name.
        # This base class is never registered directly in Beanie.
        use_revision = False
