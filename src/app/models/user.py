from datetime import datetime, timezone
from typing import List

from beanie import Indexed
from pydantic import Field

from app.models.base import DocumentBase


class User(DocumentBase):
    username: Indexed(str, unique=True) = ""  # type: ignore[valid-type]
    full_name: str = ""
    user_class: str = Field(default="", alias="class")
    is_super_admin: bool = False
    leagues_admin: List[str] = []
    leagues_viewer: List[str] = []
    leagues_member: List[str] = []
    hashed_password: str = ""
    created_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))

    class Settings:
        name = "users"

    model_config = {"populate_by_name": True}
