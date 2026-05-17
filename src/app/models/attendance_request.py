from datetime import datetime, timezone
from enum import IntEnum
from typing import Optional

from beanie import Indexed
from pydantic import Field

from app.models.base import DocumentBase


class AttendanceRequestStatus(IntEnum):
    PENDING = 0
    APPROVED = 1
    REJECTED = 2


class AttendanceRequest(DocumentBase):
    document_type: str = "AttendanceRequest"
    league_id: Indexed(str) = ""  # type: ignore[valid-type]
    transfer_window_id: str = ""
    activity_id: str = ""
    student_user_id: str = ""
    student_name: str = ""
    activity_name: str = ""
    activity_date: datetime
    status: AttendanceRequestStatus = AttendanceRequestStatus.PENDING
    requested_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    processed_at: Optional[datetime] = None
    processed_by_user_id: Optional[str] = None

    class Settings:
        name = "attendance_requests"
