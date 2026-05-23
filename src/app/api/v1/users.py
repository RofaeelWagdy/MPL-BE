# File: src/app/api/v1/users.py
from fastapi import APIRouter, Depends, HTTPException, status

from app.core.dependencies import get_current_user, get_db, require_role
from app.core.security import hash_password
from app.models.role import Role
from app.models.user import User
from app.schemas.requests import (
    AssignRoleRequest,
    RemoveRoleRequest,
    UserRegistrationRequest,
    UserUpdateRequest,
    UserLoginRequest,
)
from app.services.authorization import (
    can_user_manage_target_user,
    get_admin_accessible_league_ids,
    is_user_admin_for_league,
)
from app.services.current_user import CurrentUser
from app.services.database_service import DatabaseService
from app.core.security import hash_password, verify_password, create_access_token

router = APIRouter(prefix="/api/users", tags=["Users"])


@router.post("/register", status_code=status.HTTP_201_CREATED)
async def register_user(
    request: UserRegistrationRequest,
    db: DatabaseService = Depends(get_db),
):
    if await db.username_exists(request.username):
        raise HTTPException(
            status.HTTP_409_CONFLICT, f"Username '{request.username}' is already taken."
        )

    user = User(
        username=request.username,
        full_name=request.full_name,
        user_class=request.user_class,
        hashed_password=hash_password(request.password),
    )
    return await db.add_user(user)


@router.post("/login")
async def login(
    request: UserLoginRequest,
    db: DatabaseService = Depends(get_db),
):
    """
    Authenticate user and return a JWT token along with user details.
    """
    user = await db.get_user_by_username(request.username)
    if not user or not verify_password(request.password, user.hashed_password):
        raise HTTPException(
            status.HTTP_401_UNAUTHORIZED, "Invalid username or password"
        )

    access_token = create_access_token(data={"sub": user.username})
    return {"access_token": access_token, "token_type": "bearer", "user": user}


@router.get("/me")
async def get_current_user_profile(
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    user = await db.get_user_by_id(current_user.user_id)
    if user is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "User not found.")
    return user


@router.get("/all")
async def get_all_users(
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.LEAGUE_ADMIN)),
):
    if not current_user.is_super_admin:
        raise HTTPException(
            status.HTTP_403_FORBIDDEN, "You do not have permission to view all users."
        )
    all_users = await db.get_all_users()
    return all_users
    # all_leagues = await db.get_all_leagues()
    # all_league_ids = [league.id for league in all_leagues]
    # accessible_ids = get_admin_accessible_league_ids(current_user, all_league_ids)
    # return await db.get_all_accounts_for_leagues(accessible_ids)


@router.get("/{user_id}")
async def get_user_by_id(
    user_id: str,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    user = await db.get_user_by_id(user_id)
    if user is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, f"User '{user_id}' not found.")
    return user


@router.put("/{user_id}", status_code=status.HTTP_204_NO_CONTENT)
async def update_user(
    user_id: str,
    request: UserUpdateRequest,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.USER)),
):
    if current_user.is_viewer:
        raise HTTPException(
            status.HTTP_403_FORBIDDEN, "Viewers cannot update user profiles."
        )
    user = await db.get_user_by_id(user_id)
    if user is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, f"User '{user_id}' not found.")

    if not can_user_manage_target_user(
        current_user, user_id, list(user.leagues_member)
    ):
        raise HTTPException(
            status.HTTP_403_FORBIDDEN, "You do not have permission to update this user."
        )

    updated = False
    if request.full_name:
        user.full_name = request.full_name
        updated = True
    if request.user_class:
        user.user_class = request.user_class
        updated = True
    if request.password:
        user.hashed_password = hash_password(request.password)
        updated = True

    if updated:
        result = await db.update_user(user)
        if result is None:
            raise HTTPException(
                status.HTTP_500_INTERNAL_SERVER_ERROR, "Failed to update user."
            )


@router.post("/assign-role")
async def assign_role(
    request: AssignRoleRequest,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.LEAGUE_ADMIN)),
):
    league = await db.get_league_by_id(request.league_id)
    if league is None:
        raise HTTPException(
            status.HTTP_404_NOT_FOUND, f"League '{request.league_id}' not found."
        )

    user = await db.get_user_by_id(request.user_id)
    if user is None:
        raise HTTPException(
            status.HTTP_404_NOT_FOUND, f"User '{request.user_id}' not found."
        )

    if not is_user_admin_for_league(current_user, request.league_id):
        raise HTTPException(
            status.HTTP_403_FORBIDDEN, "You are not an admin of this league."
        )

    if request.role == "admin":
        if request.league_id not in user.leagues_admin:
            user.leagues_admin.append(request.league_id)
        if request.league_id in user.leagues_member:
            user.leagues_member.remove(request.league_id)
        if request.league_id in user.leagues_viewer:
            user.leagues_viewer.remove(request.league_id)
    elif request.role == "member":
        if request.league_id not in user.leagues_member:
            user.leagues_member.append(request.league_id)
        if request.league_id in user.leagues_admin:
            user.leagues_admin.remove(request.league_id)
        if request.league_id in user.leagues_viewer:
            user.leagues_viewer.remove(request.league_id)
    elif request.role == "viewer":
        if request.league_id not in user.leagues_viewer:
            user.leagues_viewer.append(request.league_id)
        if request.league_id in user.leagues_admin:
            user.leagues_admin.remove(request.league_id)
        if request.league_id in user.leagues_member:
            user.leagues_member.remove(request.league_id)
    else:
        raise HTTPException(
            status.HTTP_400_BAD_REQUEST, "Role must be 'admin', 'member', or 'viewer'."
        )

    updated = await db.update_user(user)
    if updated is None:
        raise HTTPException(
            status.HTTP_500_INTERNAL_SERVER_ERROR, "Failed to update user."
        )

    return {
        "message": f"Role '{request.role}' assigned for league '{request.league_id}'.",
        "user_id": user.id,
    }


@router.post("/remove-role")
async def remove_role(
    request: RemoveRoleRequest,
    db: DatabaseService = Depends(get_db),
    current_user: CurrentUser = Depends(require_role(Role.LEAGUE_ADMIN)),
):
    league = await db.get_league_by_id(request.league_id)
    if league is None:
        raise HTTPException(
            status.HTTP_404_NOT_FOUND, f"League '{request.league_id}' not found."
        )

    user = await db.get_user_by_id(request.user_id)
    if user is None:
        raise HTTPException(
            status.HTTP_404_NOT_FOUND, f"User '{request.user_id}' not found."
        )

    if not is_user_admin_for_league(current_user, request.league_id):
        raise HTTPException(
            status.HTTP_403_FORBIDDEN, "You are not an admin of this league."
        )

    removed = False
    if request.role == "admin" and request.league_id in user.leagues_admin:
        user.leagues_admin.remove(request.league_id)
        removed = True
    elif request.role == "member" and request.league_id in user.leagues_member:
        user.leagues_member.remove(request.league_id)
        removed = True
    elif request.role == "viewer" and request.league_id in user.leagues_viewer:
        user.leagues_viewer.remove(request.league_id)
        removed = True
    elif request.role not in {"admin", "member", "viewer"}:
        raise HTTPException(
            status.HTTP_400_BAD_REQUEST, "Role must be 'admin', 'member', or 'viewer'."
        )

    if removed:
        updated = await db.update_user(user)
        if updated is None:
            raise HTTPException(
                status.HTTP_500_INTERNAL_SERVER_ERROR, "Failed to update user."
            )

    return {
        "message": f"Role '{request.role}' removed.",
        "removed": removed,
        "user_id": user.id,
    }
