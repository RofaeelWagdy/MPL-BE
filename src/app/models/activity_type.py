from typing import List

from beanie import Indexed

from app.models.base import DocumentBase


class ActivityType(DocumentBase):
    name: str = ""
    default_points: int = 0
    league_id: Indexed(str) = ""  # type: ignore[valid-type]
    linked_positions: List[str] = []

    class Settings:
        name = "activity_types"
