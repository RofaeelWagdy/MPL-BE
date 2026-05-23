# File: src/app/main.py
from contextlib import asynccontextmanager
from typing import AsyncGenerator
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.security import APIKeyHeader
from fastapi import Depends
from app.core.config import get_settings
from app.core.database import connect, disconnect
from app.models.league import League
from app.models.user import User
from app.core.security import hash_password

settings = get_settings()

dev_key_scheme = APIKeyHeader(
    name="x-dev-key", auto_error=False, description="SuperAdmin Master Key"
)


async def run_seeding():
    """Seed initial league and admin user if they don't exist."""
    # Check if any leagues exist
    league_count = await League.find_all().count()
    if league_count == 0:
        new_league = League(name="E3dady League", type="ActivityPoints")
        await new_league.insert()
        league_id = new_league.id
    else:
        existing_league = await League.find_all().first_or_none()
        league_id = existing_league.id if existing_league else None

    # Check if any users exist
    user_count = await User.find_all().count()
    if user_count == 0:
        new_user = User(
            username="new_admin",
            full_name="New Admin",
            user_class="admin",
            hashed_password=hash_password("Admin@123"),
            leagues_admin=[league_id] if league_id else [],
        )
        await new_user.insert()


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncGenerator[None, None]:
    """Startup: open MongoDB connection. Shutdown: close it cleanly."""
    await connect()
    await run_seeding()
    yield
    await disconnect()


def create_app() -> FastAPI:
    app = FastAPI(
        title=settings.app_name,
        version=settings.app_version,
        docs_url="/swagger",
        redoc_url="/redoc",
        openapi_url="/openapi.json",
        debug=settings.debug,
        lifespan=lifespan,
        dependencies=[Depends(dev_key_scheme)],
    )

    # ------------------------------------------------------------------
    # CORS
    # ------------------------------------------------------------------
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"] if settings.is_development else [],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    # ------------------------------------------------------------------
    # Authentication middleware
    # Populates request.state.current_user on every request.
    # Must be added after CORSMiddleware.
    # ------------------------------------------------------------------
    from app.middlewares.authentication import AuthenticationMiddleware

    app.add_middleware(AuthenticationMiddleware)

    # ------------------------------------------------------------------
    # Routers
    # ------------------------------------------------------------------
    from app.api.v1 import (
        activity_types,
        attendance_requests,
        concrete_activities,
        leagues,
        teams,
        transfer_windows,
        users,
    )

    app.include_router(leagues.router)
    app.include_router(users.router)
    app.include_router(teams.router)
    app.include_router(transfer_windows.router)
    app.include_router(activity_types.router)
    app.include_router(concrete_activities.router)
    app.include_router(attendance_requests.router)

    # ------------------------------------------------------------------
    # Health check
    # ------------------------------------------------------------------
    @app.get("/", tags=["Health"])
    async def health_check():
        return {
            "status": "healthy",
            "app": settings.app_name,
            "version": settings.app_version,
        }

    return app


# python -m uvicorn app.main:app --reload
app = create_app()
