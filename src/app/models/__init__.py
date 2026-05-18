# Aggregates every Beanie Document model in one place.
#
# Two reasons this file has content:
#   1. database.py imports BEANIE_DOCUMENT_MODELS from here to pass
#      to init_beanie() at startup — all collections must be registered.
#   2. Routers and services can import models from one path instead of
#      hunting through individual module files.

from app.models.activity_type import ActivityType
from app.models.attendance_request import AttendanceRequest, AttendanceRequestStatus
from app.models.base import DocumentBase
from app.models.concrete_activity import ConcreteActivity
from app.models.league import League, TeamPosition
from app.models.league_ownership_state import LeagueOwnershipState
from app.models.role import Role
from app.models.team import PlayerTeamPosition, Team
from app.models.team_scores import (
    PlayerActivityScore,
    PlayerScore,
    TeamScore,
    TeamScoresResponse,
)
from app.models.transfer_window import TransferWindow
from app.models.user import User

__all__ = [
    # Beanie Documents (registered with MongoDB at startup)
    "User",
    "League",
    "TransferWindow",
    "Team",
    "LeagueOwnershipState",
    "ActivityType",
    "ConcreteActivity",
    "AttendanceRequest",
    # Embedded / non-document types
    "DocumentBase",
    "TeamPosition",
    "PlayerTeamPosition",
    "AttendanceRequestStatus",
    "Role",
    # Response-only types (plain Pydantic, not stored in MongoDB)
    "TeamScoresResponse",
    "TeamScore",
    "PlayerScore",
    "PlayerActivityScore",
]