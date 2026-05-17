from typing import Optional
from app.services.current_user import CurrentUser


def is_user_admin_for_league(user: CurrentUser, league_id: str) -> bool:
    """SuperAdmins pass always. League admins must have the league in their admin list."""
    if not league_id:
        return False
    if user.is_super_admin:
        return True
    return league_id in user.admin_leagues


def get_admin_accessible_league_ids(
    user: CurrentUser, all_league_ids: Optional[list[str]] = None
) -> list[str]:
    """SuperAdmins see all leagues. League admins only see their own."""
    if user.is_super_admin:
        return all_league_ids or []
    return user.admin_leagues


def can_user_manage_target_user(
    current_user: CurrentUser,
    target_user_id: str,
    target_user_leagues_member: list[str],
) -> bool:
    """
    A user can manage another user if:
    - They are a SuperAdmin, OR
    - They are managing themselves, OR
    - They are a LeagueAdmin for any league the target user is a member of.
    """
    if not target_user_id:
        return False
    if current_user.is_super_admin or current_user.user_id == target_user_id:
        return True
    if not current_user.is_league_admin:
        return False
    return any(league in target_user_leagues_member for league in current_user.admin_leagues)


def can_user_pick_team_from_league(user: CurrentUser, league_id: str) -> bool:
    """Only regular members can pick teams. Admins and SuperAdmins cannot."""
    if not league_id:
        return False
    if user.is_super_admin or user.is_league_admin:
        return False
    return league_id in user.member_leagues
