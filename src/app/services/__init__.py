# Re-exports the most-used service classes so routers can import
# from one place instead of knowing each module's internal path.
#
# Usage in a router:
#   from app.services import DatabaseService, CurrentUser

from app.services.authorization import (
    can_user_manage_target_user,
    can_user_pick_team_from_league,
    can_user_read_league,
    get_admin_accessible_league_ids,
    is_user_admin_for_league,
    is_user_viewer_for_league,
)
from app.services.current_user import CurrentUser
from app.services.database_service import DatabaseService

__all__ = [
    "DatabaseService",
    "CurrentUser",
    "is_user_admin_for_league",
    "is_user_viewer_for_league",
    "get_admin_accessible_league_ids",
    "can_user_read_league",
    "can_user_manage_target_user",
    "can_user_pick_team_from_league",
]