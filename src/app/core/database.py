"""
MongoDB connection lifecycle management.

Responsibilities
----------------
1. Create and own the async Motor client (one per process — Motor is
   internally thread-safe and connection-pooled).
2. Initialise Beanie with every Document model so ODM metadata is
   registered before the first request arrives.
3. Expose clean `connect` / `disconnect` coroutines that are wired into
   FastAPI's lifespan context manager in `main.py`.

Migration notes (CosmosDB → MongoDB)
-------------------------------------
- CosmosDB used separate named Containers per entity type.
  Each Beanie `Document` subclass declares its own MongoDB Collection
  via its inner `Settings` class (`collection = "CollectionName"`),
  which is the direct equivalent.
- CosmosDB Partition Keys become regular indexed fields on the documents.
  Index declarations live on the model classes themselves (see
  `src/app/models/`), keeping the schema and index strategy co-located.
- The CosmosDB SDK's `CosmosClient` is replaced by `motor.motor_asyncio.
  AsyncIOMotorClient`, which is the standard async driver for MongoDB in
  Python.  Beanie wraps Motor the same way the .NET SDK wraps the
  underlying HTTP client — all query building goes through the ODM layer.
"""

from __future__ import annotations

import logging
from typing import TYPE_CHECKING

import motor.motor_asyncio
from beanie import Document, init_beanie
from pymongo.errors import ConnectionFailure, ServerSelectionTimeoutError

from app.core.config import get_settings

if TYPE_CHECKING:
    # Import only for static analysis; avoids a circular-import at runtime.
    pass

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Module-level Motor client
# ---------------------------------------------------------------------------
# The client is intentionally a module-level singleton.  Motor manages its
# own internal connection pool, so creating one client and reusing it for
# the entire process lifetime is the correct pattern — identical to how the
# .NET CosmosClient was registered as a singleton in DI.
#
# Type is annotated as Optional so that `disconnect()` can set it to None
# for clean shutdown in test environments.
_motor_client: motor.motor_asyncio.AsyncIOMotorClient | None = None  # type: ignore[type-arg]


def get_motor_client() -> motor.motor_asyncio.AsyncIOMotorClient:  # type: ignore[type-arg]
    """
    Return the active Motor client.

    Raises RuntimeError if called before `connect()` has been awaited
    (i.e. before the FastAPI lifespan startup hook has run).
    """
    if _motor_client is None:
        raise RuntimeError(
            "MongoDB client has not been initialised. "
            "Ensure `connect()` is awaited inside the FastAPI lifespan "
            "startup hook before any request is processed."
        )
    return _motor_client


# ---------------------------------------------------------------------------
# Document model registry
# ---------------------------------------------------------------------------
# Add every Beanie Document class to this list as each model is migrated.
# Beanie uses this list during `init_beanie()` to:
#   - Register ODM metadata for each collection.
#   - Validate and create any indexes declared on the model.
#
# Import pattern: import the class from its module, then append to this list.
# Example (uncomment as models are added in later phases):
#
#   from app.models.league import League
#   from app.models.user import User
#   from app.models.transfer_window import TransferWindow
#   from app.models.team import Team
#   from app.models.league_ownership_state import LeagueOwnershipState
#   from app.models.activity_type import ActivityType
#   from app.models.concrete_activity import ConcreteActivity
#   from app.models.attendance_request import AttendanceRequest
#
BEANIE_DOCUMENT_MODELS: list[type[Document]] = [
    # Phase 2 models will be appended here progressively.
    # Do NOT remove this list even when empty; init_beanie requires it.
]


# ---------------------------------------------------------------------------
# Lifecycle helpers
# ---------------------------------------------------------------------------

async def connect() -> None:
    """
    Open the Motor connection pool and initialise the Beanie ODM.

    This coroutine must be awaited exactly once, inside the `startup`
    section of FastAPI's lifespan context manager:

        @asynccontextmanager
        async def lifespan(app: FastAPI):
            await connect()
            yield
            await disconnect()

    Motor does not actually open a socket until the first operation, but
    calling `server_info()` here acts as an eager health-check so that a
    misconfigured URI fails fast at startup rather than on the first
    incoming request.
    """
    global _motor_client  # noqa: PLW0603  (necessary pattern for module singleton)

    settings = get_settings()

    logger.info(
        "Connecting to MongoDB | uri_host=%s db=%s",
        # Log only the host portion to avoid leaking credentials.
        _redact_uri(settings.mongodb_uri_str),
        settings.mongodb_db_name,
    )

    _motor_client = motor.motor_asyncio.AsyncIOMotorClient(
        settings.mongodb_uri_str,
        # serverSelectionTimeoutMS controls how long Motor waits to find a
        # suitable server before raising ServerSelectionTimeoutError.
        # 5 s is a reasonable default for startup health-checking.
        serverSelectionTimeoutMS=5_000,
        # uuidRepresentation="standard" ensures Python uuid.UUID objects are
        # stored and retrieved consistently across driver versions.
        uuidRepresentation="standard",
    )

    try:
        # Trigger an actual network round-trip to validate the connection.
        await _motor_client.admin.command("ping")
        logger.info("MongoDB ping successful — connection pool is ready.")
    except (ConnectionFailure, ServerSelectionTimeoutError) as exc:
        logger.critical(
            "Failed to reach MongoDB at startup: %s", exc, exc_info=True
        )
        # Re-raise so the application does not start in a broken state.
        raise

    database = _motor_client[settings.mongodb_db_name]

    await init_beanie(
        database=database,
        document_models=BEANIE_DOCUMENT_MODELS,  # type: ignore[arg-type]
    )

    logger.info(
        "Beanie initialised with %d document model(s).",
        len(BEANIE_DOCUMENT_MODELS),
    )


async def disconnect() -> None:
    """
    Close the Motor connection pool gracefully.

    Must be awaited inside the `shutdown` section of FastAPI's lifespan
    context manager (see `connect()` docstring for the full pattern).
    Calling this when the client was never initialised is a safe no-op.
    """
    global _motor_client  # noqa: PLW0603

    if _motor_client is not None:
        _motor_client.close()
        _motor_client = None
        logger.info("MongoDB connection pool closed.")
    else:
        logger.debug("disconnect() called but no active Motor client found — no-op.")


# ---------------------------------------------------------------------------
# Internal helpers
# ---------------------------------------------------------------------------

def _redact_uri(uri: str) -> str:
    """
    Return a version of the MongoDB URI safe to write to logs.

    Replaces the `user:password@` section with `***:***@` so that
    credentials are never exposed in log output, while preserving enough
    of the URI for debugging (host, port, database path).
    """
    try:
        from urllib.parse import urlparse, urlunparse

        parsed = urlparse(uri)
        if parsed.username or parsed.password:
            # Rebuild netloc with redacted credentials.
            host_part = parsed.hostname or ""
            if parsed.port:
                host_part = f"{host_part}:{parsed.port}"
            redacted_netloc = f"***:***@{host_part}"
            redacted = parsed._replace(netloc=redacted_netloc)
            return urlunparse(redacted)
    except Exception:  # noqa: BLE001
        pass
    return "<uri>"
