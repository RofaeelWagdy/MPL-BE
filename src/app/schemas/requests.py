# File: src/app/schemas/requests.py
from datetime import date, datetime
from typing import Dict, List, Optional

from pydantic import BaseModel, Field

# ------------------------------------------------------------------
# League
# ------------------------------------------------------------------


class TeamPositionSchema(BaseModel):
    name: str
    count: int = Field(ge=1)


class CreateLeagueRequest(BaseModel):
    name: str
    type: str  # "ActivityPoints" or "H2H"


class ActivityPointsLeagueConfigUpdateDto(BaseModel):
    initial_budget: Optional[int] = Field(default=None, ge=0)
    team_positions: Optional[List[TeamPositionSchema]] = None
    default_player_price: Optional[int] = Field(default=None, ge=0)
    player_price_overrides: Optional[Dict[str, int]] = None
    budget_overrides: Optional[Dict[str, int]] = None
    default_ownership_cap: Optional[int] = Field(default=None, ge=0)
    member_position_ownership_caps: Optional[Dict[str, Dict[str, int]]] = None


# ------------------------------------------------------------------
# Users
# ------------------------------------------------------------------


class UserRegistrationRequest(BaseModel):
    username: str = Field(min_length=3, max_length=20)
    full_name: str = Field(min_length=2, max_length=100)
    user_class: str = Field(alias="class")
    password: str

    model_config = {"populate_by_name": True}


class UserLoginRequest(BaseModel):
    username: str = Field(..., max_length=150)
    password: str = Field(...)

class UserUpdateRequest(BaseModel):
    full_name: Optional[str] = Field(default=None, min_length=2, max_length=100)
    user_class: Optional[str] = Field(default=None, alias="class")
    password: Optional[str] = None

    model_config = {"populate_by_name": True}


class AssignRoleRequest(BaseModel):
    user_id: str
    league_id: str
    role: str  # "admin" or "member"


class RemoveRoleRequest(BaseModel):
    user_id: str
    league_id: str
    role: str  # "admin" or "member"


# ------------------------------------------------------------------
# Transfer Windows
# ------------------------------------------------------------------


class CreateTransferWindowRequest(BaseModel):
    start_date: datetime
    end_date: datetime


# ------------------------------------------------------------------
# Teams
# ------------------------------------------------------------------


class PlayerSelectionDto(BaseModel):
    player_id: str
    position: str


class PickTeamRequest(BaseModel):
    league_id: str
    players: List[PlayerSelectionDto]


# ------------------------------------------------------------------
# Activity Types
# ------------------------------------------------------------------


class CreateActivityTypeRequest(BaseModel):
    name: str
    default_points: int = Field(ge=0)
    league_id: str
    linked_positions: List[str] = []


class UpdateActivityTypeRequest(BaseModel):
    name: str
    default_points: int = Field(ge=0)
    linked_positions: List[str] = []


# ------------------------------------------------------------------
# Concrete Activities
# ------------------------------------------------------------------


class CreateConcreteActivityRequest(BaseModel):
    transfer_window_id: str
    activity_type_id: str
    date: date
    override_points: Optional[int] = None


class UpdateConcreteActivityRequest(BaseModel):
    id: str
    date: date
    override_points: Optional[int] = None


class AddParticipantRequest(BaseModel):
    participant_id: str


# ------------------------------------------------------------------
# Attendance Requests
# ------------------------------------------------------------------


class CreateAttendanceRequestRequest(BaseModel):
    league_id: str
    transfer_window_id: str
    activity_id: str
    student_user_id: str
    student_name: str
    activity_name: str
    activity_date: datetime


class ProcessAttendanceRequestRequest(BaseModel):
    request_id: str
    approve: bool
