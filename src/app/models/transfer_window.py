from datetime import datetime

from beanie import Indexed
from pydantic import Field

from app.models.base import DocumentBase


class TransferWindow(DocumentBase):
    league_id: Indexed(str) = ""  # type: ignore[valid-type]
    start_date: datetime
    end_date: datetime
    window_number: int = 0
    created_by_admin_id: str = ""

    class Settings:
        name = "transfer_windows"
