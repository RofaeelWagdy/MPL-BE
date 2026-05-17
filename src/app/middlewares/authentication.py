# File: src/app/middlewares/authentication.py
import base64
import logging

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import Response

from app.core.config import get_settings
from app.core.security import verify_password
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
      2. Authorization: Basic <base64(username:password)> → standard user login.

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

        # 2. Basic authentication
        auth_header = request.headers.get("Authorization", "")
        if auth_header.lower().startswith("basic "):
            current_user = await self._authenticate_basic(auth_header)
            if current_user:
                request.state.current_user = current_user
                return await call_next(request)
            # Bad credentials — return 401 immediately
            response = Response("Invalid username or password", status_code=401)
            return response

        # 3. No credentials — let the authorization dependency decide if that's ok
        request.state.current_user = CurrentUser()
        return await call_next(request)

    async def _authenticate_basic(self, auth_header: str) -> CurrentUser | None:
        try:
            encoded = auth_header[len("Basic "):].strip()
            decoded = base64.b64decode(encoded).decode("utf-8")
            username, password = decoded.split(":", 1)
        except Exception:
            logger.warning("Malformed Basic Auth header.")
            return None

        db = DatabaseService()
        user = await db.get_user_by_username(username)
        if user is None:
            logger.warning("Login attempt for unknown user: %s", username)
            return None

        if not verify_password(password, user.hashed_password):
            logger.warning("Wrong password for user: %s", username)
            return None

        # Users who admin at least one league are LeagueAdmins
        role = Role.LEAGUE_ADMIN if user.leagues_admin else Role.USER

        logger.info("User %s authenticated with role %s", username, role)
        return CurrentUser(
            user_id=user.id,
            username=user.username,
            role=role,
            admin_leagues=list(user.leagues_admin),
            member_leagues=list(user.leagues_member),
        )
