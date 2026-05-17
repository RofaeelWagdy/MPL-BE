# File: src/app/api/v1/attendance_requests.py
from datetime import datetime, timezone

from fastapi import APIRouter, Depends, HTTPException, Query, status

from app.core.dependencies import get_db, require_role
from app.models.attendance_request import AttendanceRequest, AttendanceRequestStatus
from app.models.role import Role
from app.schemas.requests import CreateAttendanceRequestRequest, ProcessAttendanceRequestRequest
from app.services.current_user import CurrentUser
from app.services.database_service import DatabaseService

router = APIRouter(prefix="/api/v1/attendance-requests", tags=["Attendance Requests"])


@router.post("/")
async def create_attendance_request(
    request: CreateAttendanceRequestRequest,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    # Block if student is already a participant
    activity = await db.get_concrete_activity_by_id(request.activity_id, request.transfer_window_id)
    if activity and request.student_user_id in activity.participant_ids:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "You have already attended this activity.")

    # Block duplicate pending requests
    existing = await db.get_attendance_requests_for_student(request.league_id, request.student_user_id)
    already_pending = any(
        r.activity_id == request.activity_id and r.status == AttendanceRequestStatus.PENDING
        for r in existing
    )
    if already_pending:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "You already have a pending request for this activity.")

    attendance_request = AttendanceRequest(
        league_id=request.league_id,
        transfer_window_id=request.transfer_window_id,
        activity_id=request.activity_id,
        student_user_id=request.student_user_id,
        student_name=request.student_name,
        activity_name=request.activity_name,
        activity_date=request.activity_date,
        status=AttendanceRequestStatus.PENDING,
        requested_at=datetime.now(timezone.utc),
    )
    return await db.create_attendance_request(attendance_request)


@router.get("/league/{league_id}/pending")
async def get_pending_requests(
    league_id: str,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    return await db.get_pending_attendance_requests_for_league(league_id)


@router.get("/student/{student_user_id}")
async def get_student_requests(
    student_user_id: str,
    league_id: str = Query(...),
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    return await db.get_attendance_requests_for_student(league_id, student_user_id)


@router.post("/process")
async def process_request(
    request: ProcessAttendanceRequestRequest,
    processed_by_user_id: str = Query(...),
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    attendance_request = await db.get_attendance_request_by_id(request.request_id)
    if attendance_request is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Attendance request not found.")

    if attendance_request.status != AttendanceRequestStatus.PENDING:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "This request has already been processed.")

    if request.approve:
        attendance_request.status = AttendanceRequestStatus.APPROVED
        added = await db.add_participant_to_concrete_activity(
            attendance_request.activity_id,
            attendance_request.transfer_window_id,
            attendance_request.student_user_id,
        )
        if not added:
            raise HTTPException(status.HTTP_400_BAD_REQUEST, "Failed to add participant to activity.")
    else:
        attendance_request.status = AttendanceRequestStatus.REJECTED

    attendance_request.processed_at = datetime.now(timezone.utc)
    attendance_request.processed_by_user_id = processed_by_user_id

    return await db.update_attendance_request(attendance_request)
