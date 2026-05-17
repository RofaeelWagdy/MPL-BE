# File: src/app/api/v1/transfer_windows.py
from datetime import datetime, timezone

from fastapi import APIRouter, Depends, HTTPException, status

from app.core.dependencies import get_db, require_role
from app.models.role import Role
from app.models.transfer_window import TransferWindow
from app.schemas.requests import CreateTransferWindowRequest
from app.services.authorization import is_user_admin_for_league
from app.services.current_user import CurrentUser
from app.services.database_service import DatabaseService

router = APIRouter(prefix="/api/leagues/{league_id}/transferwindows", tags=["Transfer Windows"])


@router.get("/current")
async def get_current_transfer_window(
    league_id: str,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    league = await db.get_league_by_id(league_id)
    if league is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "League not found.")

    # Check the user belongs to this league (as admin or member)
    has_access = is_user_admin_for_league(current_user, league_id) or (
        league_id in current_user.member_leagues
    )
    if not has_access:
        raise HTTPException(status.HTTP_403_FORBIDDEN, "You do not have access to this league.")

    now = datetime.now(timezone.utc)
    window = await db.get_current_transfer_window(league_id)

    if window is None:
        return {"message": "No transfer windows found", "is_active": False, "league_id": league_id, "current_time_utc": now}

    is_active = window.start_date <= now <= window.end_date

    return {
        "transfer_window": {
            "id": window.id,
            "league_id": window.league_id,
            "start_date_utc": window.start_date,
            "end_date_utc": window.end_date,
            "window_number": window.window_number,
            "created_by_admin_id": window.created_by_admin_id,
            "version": window.ver,
        },
        "is_active": is_active,
        "current_time_utc": now,
    }


@router.get("/")
async def get_all_transfer_windows(
    league_id: str,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    windows = await db.get_all_transfer_windows(league_id)
    return [
        {
            "id": w.id,
            "league_id": w.league_id,
            "start_date": w.start_date,
            "end_date": w.end_date,
            "window_number": w.window_number,
            "created_by_admin_id": w.created_by_admin_id,
        }
        for w in windows
    ]


@router.post("/create", status_code=status.HTTP_201_CREATED)
async def create_transfer_window(
    league_id: str,
    request: CreateTransferWindowRequest,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.LEAGUE_ADMIN)),
):
    league = await db.get_league_by_id(league_id)
    if league is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "League not found.")

    if not is_user_admin_for_league(current_user, league_id):
        raise HTTPException(status.HTTP_403_FORBIDDEN, "You are not an admin of this league.")

    now = datetime.now(timezone.utc)

    if request.end_date <= request.start_date:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "End date must be after start date.")

    if request.start_date <= now:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "Start date must be in the future.")

    # Block if there is already an active window
    latest = await db.get_current_transfer_window(league_id)
    if latest is not None and latest.end_date >= now:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "There is already an active transfer window.")

    window = TransferWindow(
        league_id=league_id,
        start_date=request.start_date,
        end_date=request.end_date,
        created_by_admin_id=current_user.user_id,
    )
    created = await db.create_transfer_window(window)

    return {
        "id": created.id,
        "league_id": created.league_id,
        "start_date_utc": created.start_date,
        "end_date_utc": created.end_date,
        "window_number": created.window_number,
        "created_by_admin_id": created.created_by_admin_id,
        "version": created.ver,
        "created_at": now,
    }
