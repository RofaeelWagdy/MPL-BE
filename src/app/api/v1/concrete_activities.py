# File: src/app/api/v1/concrete_activities.py
from datetime import date, datetime, timezone
from typing import Optional

from fastapi import APIRouter, Depends, HTTPException, Query, status

from app.core.dependencies import get_db, require_role
from app.models.concrete_activity import ConcreteActivity
from app.models.role import Role
from app.schemas.requests import (
    AddParticipantRequest,
    CreateConcreteActivityRequest,
    UpdateConcreteActivityRequest,
)
from app.services.current_user import CurrentUser
from app.services.database_service import DatabaseService

router = APIRouter(
    prefix="/api/v1/transfer-windows/{transfer_window_id}/concrete-activities",
    tags=["Concrete Activities"],
)


@router.get("/")
async def get_concrete_activities(
    transfer_window_id: str,
    from_date: Optional[date] = Query(default=None, alias="from"),
    to_date: Optional[date] = Query(default=None, alias="to"),
    activity_type_id: Optional[str] = Query(default=None),
    page: int = Query(default=1, ge=1),
    page_size: int = Query(default=50, ge=1),
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    page_size = min(page_size, 100)
    activities = await db.get_concrete_activities_for_transfer_window(
        transfer_window_id, from_date, to_date, activity_type_id
    )
    start = (page - 1) * page_size
    return activities[start : start + page_size]


@router.post("/", status_code=status.HTTP_201_CREATED)
async def create_concrete_activity(
    transfer_window_id: str,
    request: CreateConcreteActivityRequest,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    if request.transfer_window_id != transfer_window_id:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "transfer_window_id in body must match the route.")

    activity_type = await db.get_activity_type_by_id(request.activity_type_id)
    if activity_type is None:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "Activity type not found.")

    activity = ConcreteActivity(
        transfer_window_id=transfer_window_id,
        activity_type_id=request.activity_type_id,
        date=request.date,
        override_points=request.override_points,
    )
    return await db.add_concrete_activity(activity)


@router.post("/{activity_id}")
async def update_concrete_activity(
    transfer_window_id: str,
    activity_id: str,
    request: UpdateConcreteActivityRequest,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    if request.id != activity_id:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "Activity ID in body must match the route.")

    existing = await db.get_concrete_activity_by_id(activity_id, transfer_window_id)
    if existing is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Concrete activity not found.")

    existing.date = request.date
    existing.override_points = request.override_points
    existing.updated_at = datetime.now(timezone.utc)

    return await db.update_concrete_activity(existing)


@router.delete("/{activity_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_concrete_activity(
    transfer_window_id: str,
    activity_id: str,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    existing = await db.get_concrete_activity_by_id(activity_id, transfer_window_id)
    if existing is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Concrete activity not found.")

    deleted = await db.delete_concrete_activity(activity_id, transfer_window_id)
    if not deleted:
        raise HTTPException(status.HTTP_500_INTERNAL_SERVER_ERROR, "Failed to delete concrete activity.")


@router.post("/{activity_id}/participants")
async def add_participant(
    transfer_window_id: str,
    activity_id: str,
    request: AddParticipantRequest,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    if not request.participant_id.strip():
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "participant_id is required.")

    success = await db.add_participant_to_concrete_activity(activity_id, transfer_window_id, request.participant_id)
    if not success:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Concrete activity not found.")

    return {"message": "Participant added."}


@router.delete("/{activity_id}/participants/{participant_id}")
async def remove_participant(
    transfer_window_id: str,
    activity_id: str,
    participant_id: str,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    success = await db.remove_participant_from_concrete_activity(activity_id, transfer_window_id, participant_id)
    if not success:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Concrete activity not found.")

    return {"message": "Participant removed."}
