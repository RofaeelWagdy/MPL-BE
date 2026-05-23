# File: src/app/middlewares/authentication.py
import logging

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import Response

from app.core.config import get_settings
from app.core.security import verify_access_token
from app.models.role import Role
from app.services.current_user import CurrentUser
from app.services.database_service import DatabaseService

logger = logging.getLogger(__name__)
settings = get_settings()

DEV_KEY_HEADER = "x-dev-key"


class AuthenticationMiddleware(BaseHTTPMiddleware):
    """
    Runs on every request before the router.
    Mirrors AuthenticationMiddleware.cs — populates request.state.current_user.

    Two auth methods supported:
      1. x-dev-key header → SuperAdmin access (dev/admin tool only).
      2. Authorization: Bearer <jwt_token> → standard user login.

    If no valid credentials are provided, current_user is set to a public
    (unauthenticated) user. The authorization layer (dependencies) decides
    whether that is sufficient for the requested endpoint.
    """

    async def dispatch(self, request: Request, call_next) -> Response:
        # 1. Dev key → SuperAdmin
        provided_key = request.headers.get(DEV_KEY_HEADER, "")
        if provided_key and settings.dev_key and provided_key == settings.dev_key:
            logger.info("SuperAdmin authenticated via dev key: %s", request.url.path)
            request.state.current_user = CurrentUser(
                user_id="superadmin",
                username="superadmin",
                role=Role.SUPER_ADMIN,
            )
            return await call_next(request)

        # 2. Bearer authentication
        auth_header = request.headers.get("Authorization", "")
        if auth_header.lower().startswith("bearer "):
            current_user = await self._authenticate_bearer(auth_header)
            if current_user:
                request.state.current_user = current_user
                return await call_next(request)
            # Bad credentials — return 401 immediately
            response = Response("Invalid token", status_code=401)
            return response

        # 3. No credentials — let the authorization dependency decide if that's ok
        request.state.current_user = CurrentUser()
        return await call_next(request)

    async def _authenticate_bearer(self, auth_header: str) -> CurrentUser | None:
        try:
            token = auth_header[len("Bearer "):].strip()
            payload = verify_access_token(token)
            if not payload:
                logger.warning("Invalid JWT token.")
                return None
            username = payload.get("sub")
            if not username:
                return None
        except Exception:
            logger.warning("Malformed Bearer Auth header.")
            return None

        db = DatabaseService()
        user = await db.get_user_by_username(username)
        if user is None:
            logger.warning("Login attempt for unknown user: %s", username)
            return None

        role = Role.USER
        if user.is_super_admin:
            role = Role.SUPER_ADMIN
        elif user.leagues_admin:
            role = Role.LEAGUE_ADMIN
        elif user.leagues_viewer:
            role = Role.VIEWER

        return CurrentUser(
            user_id=user.id,
            username=user.username,
            role=role,
            admin_leagues=list(user.leagues_admin),
            viewer_leagues=list(user.leagues_viewer),
            member_leagues=list(user.leagues_member),
        )
