from datetime import date, datetime, timezone
from typing import List, Optional

from beanie import Indexed
from pydantic import Field

from app.models.base import DocumentBase


class ConcreteActivity(DocumentBase):
    document_type: str = "ConcreteActivity"
    transfer_window_id: Indexed(str) = ""  # type: ignore[valid-type]
    activity_type_id: str = ""
    date: date
    override_points: Optional[int] = None
    participant_ids: List[str] = []
    created_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    updated_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))

    class Settings:
        name = "concrete_activities"
