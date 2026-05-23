from enum import IntEnum


class Role(IntEnum):
    # User permission levels. Higher value = more access.
    PUBLIC = 0
    USER = 1
    VIEWER = 2
    LEAGUE_ADMIN = 3
    SUPER_ADMIN = 4
