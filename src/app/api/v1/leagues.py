# File: src/app/api/v1/leagues.py
from fastapi import APIRouter, Depends, HTTPException, status

from app.core.dependencies import get_db, require_role
from app.models.league import League, TeamPosition
from app.models.role import Role
from app.schemas.requests import ActivityPointsLeagueConfigUpdateDto, CreateLeagueRequest
from app.services.authorization import can_user_read_league, is_user_admin_for_league
from app.services.current_user import CurrentUser
from app.services.database_service import DatabaseService

router = APIRouter(prefix="/api/leagues", tags=["Leagues"])


@router.post("/", status_code=status.HTTP_201_CREATED, response_model=League)
async def create_league(
    request: CreateLeagueRequest,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.SUPER_ADMIN)),
):
    if request.type not in ("ActivityPoints", "H2H"):
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "Type must be 'ActivityPoints' or 'H2H'")

    if await db.league_name_exists(request.name):
        raise HTTPException(status.HTTP_409_CONFLICT, f"A league named '{request.name}' already exists.")

    league = League(name=request.name, type=request.type)
    return await db.add_league(league)

@router.get("", response_model=list[League])
async def get_all_leagues(
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    if not current_user.is_super_admin:
        raise HTTPException(status.HTTP_403_FORBIDDEN, "You do not have permission to view all leagues.")
    return await db.get_all_leagues()

@router.get("/{league_id}", response_model=League)
async def get_league_by_id(
    league_id: str,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    if not can_user_read_league(current_user, league_id):
        raise HTTPException(status.HTTP_403_FORBIDDEN, "You do not have access to this league.")
    league = await db.get_league_by_id(league_id)
    if league is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, f"League '{league_id}' not found.")
    return league


@router.put("/{league_id}/activitypoints-config", status_code=status.HTTP_200_OK)
async def update_activity_points_config(
    league_id: str,
    config: ActivityPointsLeagueConfigUpdateDto,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.LEAGUE_ADMIN)),
):
    league = await db.get_league_by_id(league_id)
    if league is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, f"League '{league_id}' not found.")

    if league.type != "ActivityPoints":
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "This config only applies to ActivityPoints leagues.")

    if not is_user_admin_for_league(current_user, league_id):
        raise HTTPException(status.HTTP_403_FORBIDDEN, "You are not an admin of this league.")

    updated = False

    if config.team_positions is not None:
        # Validate: all positions must have a name and count >= 1
        if any(not p.name.strip() or p.count < 1 for p in config.team_positions):
            raise HTTPException(status.HTTP_400_BAD_REQUEST, "All positions must have a name and a positive count.")

        # Validate: no duplicate position names (case-insensitive)
        names = [p.name.lower() for p in config.team_positions]
        if len(names) != len(set(names)):
            raise HTTPException(status.HTTP_400_BAD_REQUEST, "Position names must be unique.")

        league.team_positions = [TeamPosition(name=p.name, count=p.count) for p in config.team_positions]
        updated = True

    if config.initial_budget is not None:
        league.initial_budget = config.initial_budget
        updated = True

    if config.default_player_price is not None:
        league.default_player_price = config.default_player_price
        updated = True

    if config.player_price_overrides is not None:
        if any(v < 0 for v in config.player_price_overrides.values()):
            raise HTTPException(status.HTTP_400_BAD_REQUEST, "All prices in player_price_overrides must be non-negative.")
        league.player_price_overrides = config.player_price_overrides
        updated = True

    if config.budget_overrides is not None:
        if any(v <= 0 for v in config.budget_overrides.values()):
            raise HTTPException(status.HTTP_400_BAD_REQUEST, "All values in budget_overrides must be positive.")
        league.budget_overrides = config.budget_overrides
        updated = True

    if config.default_ownership_cap is not None:
        league.default_ownership_cap = config.default_ownership_cap
        updated = True

    if config.member_position_ownership_caps is not None:
        for member_id, position_caps in config.member_position_ownership_caps.items():
            if not member_id.strip():
                raise HTTPException(status.HTTP_400_BAD_REQUEST, "Member user ID cannot be empty.")
            if not position_caps:
                raise HTTPException(status.HTTP_400_BAD_REQUEST, f"Position caps for member '{member_id}' cannot be empty.")
            for pos_name, cap in position_caps.items():
                if not pos_name.strip():
                    raise HTTPException(status.HTTP_400_BAD_REQUEST, "Position name cannot be empty.")
                if cap < 0:
                    raise HTTPException(status.HTTP_400_BAD_REQUEST, "Ownership cap values must be non-negative.")
                # Validate position name against league's defined positions
                if league.team_positions:
                    valid = [p.name for p in league.team_positions]
                    if pos_name not in valid:
                        raise HTTPException(
                            status.HTTP_400_BAD_REQUEST,
                            f"Position '{pos_name}' is not defined in this league's team structure.",
                        )
        league.member_position_ownership_caps = config.member_position_ownership_caps
        updated = True

    if updated:
        success = await db.update_league(league)
        if not success:
            raise HTTPException(status.HTTP_500_INTERNAL_SERVER_ERROR, "Failed to save league configuration.")
