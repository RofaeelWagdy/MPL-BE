# File: src/app/api/v1/activity_types.py
from fastapi import APIRouter, Depends, HTTPException, status

from app.core.dependencies import get_db, require_role
from app.models.activity_type import ActivityType
from app.models.role import Role
from app.schemas.requests import CreateActivityTypeRequest, UpdateActivityTypeRequest
from app.services.current_user import CurrentUser
from app.services.database_service import DatabaseService

router = APIRouter(prefix="/api/activitytypes", tags=["Activity Types"])


@router.get("/league/{league_id}")
async def get_activity_types_for_league(
    league_id: str,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    return await db.get_activity_types_for_league(league_id)


@router.post("", status_code=status.HTTP_201_CREATED)
async def create_activity_type(
    request: CreateActivityTypeRequest,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.LEAGUE_ADMIN)),
):
    activity_type = ActivityType(
        name=request.name.strip(),
        default_points=request.default_points,
        league_id=request.league_id,
        linked_positions=list(set(request.linked_positions)),
    )
    return await db.add_activity_type(activity_type)


@router.put("/{activity_type_id}")
async def update_activity_type(
    activity_type_id: str,
    request: UpdateActivityTypeRequest,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.LEAGUE_ADMIN)),
):
    existing = await db.get_activity_type_by_id(activity_type_id)
    if existing is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, f"Activity type '{activity_type_id}' not found.")

    existing.name = request.name.strip()
    existing.default_points = request.default_points
    existing.linked_positions = list(set(request.linked_positions))

    result = await db.update_activity_type(existing)
    if result is None:
        raise HTTPException(status.HTTP_500_INTERNAL_SERVER_ERROR, "Failed to update activity type.")
    return result
