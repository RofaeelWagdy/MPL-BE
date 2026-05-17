"""
Shared FastAPI dependency providers.

This module centralises all `Depends(...)` callables so that routers and
services import from one place rather than re-declaring the same logic.

.NET equivalent: the DI container registrations in Program.cs
  builder.Services.AddSingleton<ICosmosDbService, LiteDbService>();
  builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
"""

from app.core.config import Settings, get_settings  # noqa: F401 – re-exported

# ---------------------------------------------------------------------------
# Settings dependency
# ---------------------------------------------------------------------------
# Usage in a router or service:
#   from app.core.dependencies import get_settings
#   settings: Settings = Depends(get_settings)
#
# `get_settings` is already imported above from config.py; it is
# re-exported here so callers have a single consistent import path.

# ---------------------------------------------------------------------------
# Future service dependencies (added progressively in later phases)
# ---------------------------------------------------------------------------
# Example pattern once services are migrated:
#
#   from app.services.user_service import UserService
#
#   async def get_user_service() -> UserService:
#       return UserService()
#
# Then in a router:
#   user_service: UserService = Depends(get_user_service)
