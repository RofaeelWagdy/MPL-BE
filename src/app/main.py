# File: src/app/main.py
from contextlib import asynccontextmanager
from typing import AsyncGenerator
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.core.config import get_settings
from app.core.database import connect, disconnect

settings = get_settings()


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncGenerator[None, None]:
    """Startup: open MongoDB connection. Shutdown: close it cleanly."""
    await connect()
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
        return {"status": "healthy", "app": settings.app_name, "version": settings.app_version}

    return app


app = create_app()
