from enum import IntEnum


class Role(IntEnum):
    # User permission levels. Higher value = more access.
    PUBLIC = 0
    USER = 1
    LEAGUE_ADMIN = 2
    SUPER_ADMIN = 3
