"""
FastAPI application factory.

This module is the Python equivalent of .NET's Program.cs / Startup.cs.
It wires together:
  - Application metadata (name, version, OpenAPI docs).
  - The MongoDB lifespan (connect on startup, disconnect on shutdown).
  - Global middleware (CORS, authentication, authorisation).
  - All versioned API routers (added progressively in later phases).
"""

from __future__ import annotations

from contextlib import asynccontextmanager
from typing import AsyncGenerator

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.core.config import get_settings
from app.core.database import connect, disconnect

settings = get_settings()


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
    """
    Construct and configure the FastAPI application instance.

    Keeping construction inside a factory function (rather than at module
    level) makes the app trivially importable in tests without side-effects,
    and matches the common Python factory pattern.
    """
    app = FastAPI(
        title=settings.app_name,
        version=settings.app_version,
        # Mirrors the Swagger UI that the .NET project exposed at /swagger.
        docs_url="/swagger",
        redoc_url="/redoc",
        openapi_url="/openapi.json",
        debug=settings.debug,
        lifespan=lifespan,
    )

    # ------------------------------------------------------------------
    # CORS
    # Adjust origins / methods / headers to match your frontend deployment.
    # ------------------------------------------------------------------
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"] if settings.is_development else [],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    # ------------------------------------------------------------------
    # Routers
    # Uncomment and import each router as it is migrated in later phases.
    # ------------------------------------------------------------------
    # from app.api.v1 import users, leagues, teams, transfer_windows
    # from app.api.v1 import activity_types, concrete_activities, attendance_requests
    #
    # API_PREFIX = "/api"
    # app.include_router(users.router, prefix=API_PREFIX)
    # app.include_router(leagues.router, prefix=API_PREFIX)
    # app.include_router(teams.router, prefix=API_PREFIX)
    # app.include_router(transfer_windows.router, prefix=API_PREFIX)
    # app.include_router(activity_types.router, prefix=API_PREFIX)
    # app.include_router(concrete_activities.router, prefix=API_PREFIX)
    # app.include_router(attendance_requests.router, prefix=API_PREFIX)

    return app


# ---------------------------------------------------------------------------
# Module-level app instance
# ---------------------------------------------------------------------------
# Uvicorn / Gunicorn expect to import `app` from this module.
# Entry point in pyproject.toml / Dockerfile:
#   uvicorn src.app.main:app --host 0.0.0.0 --port 8080
app = create_app()
