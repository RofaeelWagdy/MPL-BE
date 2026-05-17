from dataclasses import dataclass, field
from app.models.role import Role


@dataclass
class CurrentUser:
    """
    Holds the authenticated user's identity and permissions for a single request.
    This is the Python equivalent of .NET's ClaimsPrincipal — it replaces the
    claims-based system and is populated by the authentication middleware.
    """

    user_id: str = ""
    username: str = ""
    role: Role = Role.PUBLIC
    admin_leagues: list[str] = field(default_factory=list)
    member_leagues: list[str] = field(default_factory=list)

    @property
    def is_super_admin(self) -> bool:
        return self.role == Role.SUPER_ADMIN

    @property
    def is_league_admin(self) -> bool:
        return self.role == Role.LEAGUE_ADMIN

    @property
    def is_authenticated(self) -> bool:
        return self.role >= Role.USER
