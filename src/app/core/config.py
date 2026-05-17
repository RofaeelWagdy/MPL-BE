"""
Application configuration management.

Reads settings from environment variables (and an optional .env file).
Mirrors the concerns previously spread across appsettings.json,
appsettings.Development.json, and launchSettings.json in the .NET project.
"""

from functools import lru_cache
from typing import Literal

from pydantic import Field, MongoDsn, computed_field, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """
    All application settings are declared here as typed fields.

    Priority order (highest → lowest):
      1. Actual environment variables
      2. Variables declared in the .env file (path controlled by `env_file`)
      3. Default values defined below

    The `model_config` block instructs pydantic-settings to:
      - Look for a `.env` file in the working directory.
      - Ignore extra keys that appear in the environment / .env file.
      - Treat field names as case-insensitive when matching env vars.
    """

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
        case_sensitive=False,
    )

    # .end Fields
    app_name: str = Field(
        default="Morkosia PrepaLeague API",
        description="Human-readable application name surfaced in OpenAPI docs.",
    )
    app_version: str = Field(
        default="1.0.0",
        description="Semantic version string surfaced in OpenAPI docs.",
    )
    environment: Literal["development", "production", "test"] = Field(
        default="development",
        description=(
            "Runtime environment. Controls debug behaviour and which "
            "secondary config values (e.g. dev_key) are active."
        ),
    )
    debug: bool = Field(
        default=False,
        description="Enable FastAPI/Uvicorn debug mode. Never True in production.",
    )

    # ------------------------------------------------------------------
    # MongoDB / Motor / Beanie
    # Previously: CosmosDb:ConnectionString + CosmosDb:DatabaseName in
    # appsettings.json, and LiteDb:Path in the local dev version.
    # ------------------------------------------------------------------
    mongodb_uri: MongoDsn = Field(
        default="mongodb://localhost:27017",  # type: ignore[assignment]
        description=(
            "Full MongoDB connection URI.  "
            "Example (Atlas): mongodb+srv://user:pass@cluster.mongodb.net"
        ),
    )
    mongodb_db_name: str = Field(
        default="mpl",
        description="Name of the MongoDB database (mirrors CosmosDb:DatabaseName).",
    )

    # ------------------------------------------------------------------
    # Authentication
    # Previously: Development:DevKey / Production:DevKey in
    # appsettings.*.json, validated inside AuthenticationMiddleware.
    # ------------------------------------------------------------------
    dev_key: str = Field(
        default="",
        description=(
            "Secret header value that grants SuperAdmin access when sent as "
            "'x-dev-key'. Must be a long random string in production."
        ),
    )

    # ------------------------------------------------------------------
    # Security / JWT (reserved for future use; BasicAuth is current scheme)
    # ------------------------------------------------------------------
    secret_key: str = Field(
        default="change-me-in-production",
        description="Secret used to sign any future JWT tokens.",
    )
    access_token_expire_minutes: int = Field(
        default=60 * 24,  # 24 hours
        description="JWT access token lifetime in minutes.",
    )

    # ------------------------------------------------------------------
    # Computed helpers
    # ------------------------------------------------------------------
    @computed_field  # type: ignore[misc]
    @property
    def is_development(self) -> bool:
        """Convenience flag; avoids string comparisons at call sites."""
        return self.environment == "development"

    @computed_field  # type: ignore[misc]
    @property
    def mongodb_uri_str(self) -> str:
        """
        Motor requires a plain `str`, not a `MongoDsn` object.
        This computed field handles that cast transparently.
        """
        return str(self.mongodb_uri)

    # ------------------------------------------------------------------
    # Cross-field validation
    # ------------------------------------------------------------------
    @model_validator(mode="after")
    def _validate_production_secrets(self) -> "Settings":
        """
        Enforce that obviously insecure defaults are not used in production.
        Raises ValueError at startup rather than letting a badly configured
        production deployment run silently.
        """
        if self.environment == "production":
            if not self.dev_key or len(self.dev_key) < 32:
                raise ValueError(
                    "dev_key must be at least 32 characters long in production."
                )
            if self.secret_key == "change-me-in-production":
                raise ValueError(
                    "secret_key must be changed from the default value in production."
                )
        return self


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    """
    Return a cached singleton Settings instance.

    Using `lru_cache` ensures the .env file is read exactly once per
    process lifetime, which is the correct behaviour for a long-running
    ASGI application.

    Usage in a FastAPI dependency:
        from app.core.config import get_settings
        settings: Settings = Depends(get_settings)

    Usage outside a request context (e.g. database.py):
        from app.core.config import get_settings
        settings = get_settings()
    """
    return Settings()
