# File: src/app/api/v1/teams.py
import asyncio
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException, Query, status

from app.core.dependencies import get_db, require_role
from app.models.league import League
from app.models.league_ownership_state import LeagueOwnershipState
from app.models.role import Role
from app.models.team import PlayerTeamPosition, Team
from app.models.team_scores import (
    PlayerActivityScore,
    PlayerScore,
    TeamScore,
    TeamScoresResponse,
)
from app.schemas.requests import PickTeamRequest
from app.services.authorization import can_user_pick_team_from_league
from app.services.current_user import CurrentUser
from app.services.database_service import DatabaseService

router = APIRouter(prefix="/api/teams", tags=["Teams"])


@router.get("/available-players/{league_id}")
async def get_available_players(
    league_id: str,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    league = await db.get_league_by_id(league_id)
    if league is None:
        raise HTTPException(
            status.HTTP_404_NOT_FOUND, f"League '{league_id}' not found."
        )

    all_users = await db.get_all_users_for_leagues([league_id])
    position_counts = await db.get_player_position_ownership_counts(league_id)

    # Subtract the current user's own team selections so availability
    # reflects what they can pick, not counting their existing picks.
    if current_user.user_id:
        position_counts = await _get_adjusted_counts(
            db, league_id, current_user.user_id, position_counts
        )

    available_positions = (
        [p.name for p in league.team_positions] if league.team_positions else ["PLAYER"]
    )

    return [
        {
            "id": user.id,
            "username": user.username,
            "full_name": user.full_name,
            "user_class": user.user_class,
            "price": league.get_player_price(user.id),
            "positions": [
                {
                    "position": pos,
                    "is_available": _is_available(
                        league, user.id, pos, position_counts
                    ),
                    "current_ownership": position_counts.get(user.id, {}).get(pos, 0),
                    "ownership_cap": league.get_effective_ownership_cap(user.id, pos),
                }
                for pos in available_positions
            ],
        }
        for user in all_users
    ]


@router.get("/my-team/{league_id}")
async def get_my_team(
    league_id: str,
    window_id: Optional[str] = Query(default=None),
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    if window_id:
        window = await db.get_transfer_window_by_id(league_id, window_id)
    else:
        window = await db.get_current_transfer_window(league_id)

    if window is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "No transfer window found.")

    team = await db.get_manager_team_for_transfer_window(
        league_id, window.id, current_user.user_id
    )
    if team is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "No team found for this window.")

    return team


@router.get("/scores/{league_id}", response_model=TeamScoresResponse)
async def get_team_scores(
    league_id: str,
    window_id: Optional[str] = Query(default=None),
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    league = await db.get_league_by_id(league_id)
    if league is None:
        raise HTTPException(
            status.HTTP_404_NOT_FOUND, f"League '{league_id}' not found."
        )

    all_windows = await db.get_all_transfer_windows(league_id)
    if not all_windows:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "No transfer windows found.")

    # Decide which windows to score
    if window_id:
        target_window = await db.get_transfer_window_by_id(league_id, window_id)
        windows_to_score = [target_window] if target_window else []
    else:
        windows_to_score = all_windows

    # Gather all teams and activities for the selected windows
    all_teams = []
    all_activities = []
    for w in windows_to_score:
        all_teams.extend(await db.get_teams_for_transfer_window(league_id, w.id))
        all_activities.extend(
            await db.get_concrete_activities_for_transfer_window(w.id)
        )

    activity_types = await db.get_activity_types_for_league(league_id)
    all_users = await db.get_all_users_for_leagues([league_id])

    activity_type_map = {at.id: at for at in activity_types}
    user_map = {u.id: u for u in all_users}

    team_scores: list[TeamScore] = []

    for team in all_teams:
        manager = user_map.get(team.manager_user_id)
        manager_name = (
            manager.full_name or manager.username if manager else team.manager_user_id
        )

        player_scores: dict[str, PlayerScore] = {}
        for player in team.players:
            pu = user_map.get(player.player_id)
            player_scores[player.player_id] = PlayerScore(
                player_id=player.player_id,
                player_name=pu.full_name or pu.username if pu else player.player_id,
                position=player.position,
            )

        for activity in all_activities:
            # Only score activities that belong to this team's window
            if activity.transfer_window_id != team.transfer_window_id:
                continue
            activity_type = activity_type_map.get(activity.activity_type_id)
            if not activity_type:
                continue

            points = (
                activity.override_points
                if activity.override_points is not None
                else activity_type.default_points
            )
            linked = activity_type.linked_positions or []

            for ps in player_scores.values():
                participated = ps.player_id in activity.participant_ids
                qualifies = participated and ps.position in linked
                ps.activities.append(
                    PlayerActivityScore(
                        activity_id=activity.id,
                        transfer_window_id=activity.transfer_window_id,
                        activity_name=activity_type.name,
                        date=str(activity.date),
                        points=points if qualifies else 0,
                        potential_points=points,
                        qualifies=qualifies,
                        participated=participated,
                    )
                )

        players_list = list(player_scores.values())
        total = sum(a.points for p in players_list for a in p.activities)

        team_scores.append(
            TeamScore(
                team_id=team.id,
                transfer_window_id=team.transfer_window_id,
                manager_user_id=team.manager_user_id,
                manager_name=manager_name,
                total_score=total,
                week_score=total,
                players=players_list,
            )
        )

    return TeamScoresResponse(teams=team_scores)


@router.post("/pick-team")
async def pick_team(
    request: PickTeamRequest,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    if not can_user_pick_team_from_league(current_user, request.league_id):
        raise HTTPException(
            status.HTTP_403_FORBIDDEN, "Only league members can pick teams."
        )

    league = await db.get_league_by_id(request.league_id)
    if league is None:
        raise HTTPException(
            status.HTTP_404_NOT_FOUND, f"League '{request.league_id}' not found."
        )

    window = await db.get_current_transfer_window(request.league_id)
    if window is None:
        raise HTTPException(
            status.HTTP_400_BAD_REQUEST, "No active transfer window for this league."
        )

    team = Team(
        league_id=request.league_id,
        manager_user_id=current_user.user_id,
        transfer_window_id=window.id,
        players=[
            PlayerTeamPosition(player_id=p.player_id, position=p.position)
            for p in request.players
        ],
    )

    # --- Validate positional structure ---
    _validate_positions(team, league)

    # --- Validate all players are league members ---
    all_users = await db.get_all_users_for_leagues([league.id])
    valid_ids = {u.id for u in all_users}
    for player in team.players:
        if player.player_id not in valid_ids:
            raise HTTPException(
                status.HTTP_400_BAD_REQUEST,
                f"Player '{player.player_id}' is not a member of this league.",
            )

    # --- Validate budget ---
    budget = league.get_effective_budget(current_user.user_id)
    if budget is not None:
        total_cost = sum(league.get_player_price(p.player_id) for p in team.players)
        if total_cost > budget:
            raise HTTPException(
                status.HTTP_400_BAD_REQUEST,
                f"Team cost ({total_cost}) exceeds your budget ({budget}).",
            )

    # --- Validate ownership caps with optimistic concurrency retry ---
    max_retries = 3
    for attempt in range(max_retries):
        ownership_state, etag = await db.get_league_ownership_state_with_etag(
            league.id, window.id
        )

        if ownership_state is None:
            ownership_state = await db.initialize_league_ownership_state(
                league.id, window.id
            )
            ownership_state, etag = await db.get_league_ownership_state_with_etag(
                league.id, window.id
            )

        _validate_ownership_caps(team, league, ownership_state, current_user.user_id)

        success = await db.save_team_with_ownership_update(
            team, ownership_state, etag or ""
        )
        if success:
            return team

        if attempt < max_retries - 1:
            await asyncio.sleep(0.1 + attempt * 0.05)

    raise HTTPException(
        status.HTTP_409_CONFLICT, "Concurrent update conflict — please try again."
    )


# ------------------------------------------------------------------
# Private helpers
# ------------------------------------------------------------------


def _is_available(
    league: League,
    player_id: str,
    position: str,
    position_counts: dict[str, dict[str, int]],
) -> bool:
    cap = league.get_effective_ownership_cap(player_id, position)
    if cap is None:
        return True
    current = position_counts.get(player_id, {}).get(position, 0)
    return current < cap


def _validate_positions(team: Team, league: League) -> None:
    if not league.team_positions:
        raise HTTPException(
            status.HTTP_400_BAD_REQUEST, "League has no defined team structure."
        )

    # No duplicate players
    player_ids = [p.player_id for p in team.players]
    if len(player_ids) != len(set(player_ids)):
        raise HTTPException(
            status.HTTP_400_BAD_REQUEST,
            "A player cannot be selected more than once per team.",
        )

    # Count by position
    counts: dict[str, int] = {}
    for p in team.players:
        counts[p.position] = counts.get(p.position, 0) + 1

    for required in league.team_positions:
        actual = counts.get(required.name, 0)
        if actual != required.count:
            raise HTTPException(
                status.HTTP_400_BAD_REQUEST,
                f"Position '{required.name}' requires {required.count} player(s), got {actual}.",
            )

    valid_positions = {p.name for p in league.team_positions}
    invalid = [pos for pos in counts if pos not in valid_positions]
    if invalid:
        raise HTTPException(
            status.HTTP_400_BAD_REQUEST, f"Invalid positions: {invalid}"
        )


def _validate_ownership_caps(
    team: Team,
    league: League,
    ownership_state: LeagueOwnershipState,
    manager_user_id: str,
) -> None:
    # Work on a copy of the current counts so we can simulate adding this team
    working: dict[str, dict[str, int]] = {
        pid: dict(pos_map) for pid, pos_map in ownership_state.ownership_counts.items()
    }

    for player in team.players:
        pid, pos = player.player_id, player.position
        working.setdefault(pid, {})
        current = working[pid].get(pos, 0)
        cap = league.get_effective_ownership_cap(pid, pos)

        if cap is not None and current >= cap:
            raise HTTPException(
                status.HTTP_400_BAD_REQUEST,
                f"Player '{pid}' cannot be selected as '{pos}' — ownership cap of {cap} reached.",
            )
        working[pid][pos] = current + 1


async def _get_adjusted_counts(
    db: DatabaseService,
    league_id: str,
    manager_user_id: str,
    counts: dict[str, dict[str, int]],
) -> dict[str, dict[str, int]]:
    """Remove the current manager's existing team from the ownership counts."""
    adjusted = {pid: dict(pos_map) for pid, pos_map in counts.items()}
    window = await db.get_current_transfer_window(league_id)
    if window is None:
        return adjusted

    team = await db.get_manager_team_for_transfer_window(
        league_id, window.id, manager_user_id
    )
    if team is None:
        return adjusted

    for player in team.players:
        pid, pos = player.player_id, player.position
        if pid in adjusted and pos in adjusted[pid]:
            adjusted[pid][pos] -= 1
            if adjusted[pid][pos] <= 0:
                del adjusted[pid][pos]

    return adjusted
