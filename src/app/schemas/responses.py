from pydantic import BaseModel


class LeagueSummaryResponse(BaseModel):
    id: str
    name: str
    type: str
    admins_count: int = 0
    members_count: int = 0
    viewers_count: int = 0
