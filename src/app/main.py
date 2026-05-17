from __future__ import annotations
from contextlib import asynccontextmanager
from typing import AsyncGenerator
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.security import APIKeyHeader
from fastapi import Depends
from app.core.config import get_settings
from app.core.database import connect, disconnect
from app.middlewares.authentication import AuthenticationMiddleware

settings = get_settings()

dev_key_scheme = APIKeyHeader(
    name="x-dev-key", auto_error=False, description="SuperAdmin Master Key"
)


# ---------------------------------------------------------------------------
# Lifespan context manager
# ---------------------------------------------------------------------------
# FastAPI's recommended approach for startup / shutdown logic since v0.93.
# Replaces the deprecated @app.on_event("startup") pattern.
# ---------------------------------------------------------------------------
@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncGenerator[None, None]:
    """Application lifespan: runs setup before yield, teardown after."""
    # --- Startup ---
    await connect()
    yield  # Application is running and serving requests here.
    # --- Shutdown ---
    await disconnect()


# ---------------------------------------------------------------------------
# Application factory
# ---------------------------------------------------------------------------
def create_app() -> FastAPI:
    # Construct and configure the FastAPI application instance.
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

    # Add CORS middleware to allow requests from the frontend
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"] if settings.is_development else [],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    # --- Authentication middleware ---
    app.add_middleware(AuthenticationMiddleware)

    # --- Routers ---
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

    return app


# ---------------------------------------------------------------------------
# Module-level app instance
# ---------------------------------------------------------------------------
# Uvicorn / Gunicorn expect to import `app` from this module.
# Entry point in pyproject.toml / Dockerfile:
#   uvicorn src.app.main:app --host 0.0.0.0 --port 8080

#   python -m uvicorn app.main:app --reload
app = create_app()
