from typing import List

from beanie import Indexed
from pydantic import BaseModel

from app.models.base import DocumentBase


class PlayerTeamPosition(BaseModel):
    # A single player slot inside a team.

    player_id: str = ""
    position: str = ""


class Team(DocumentBase):
    # The `id` is a composite string: "{manager_user_id}_{transfer_window_id}".
    # This enforces one team per manager per transfer window, exactly as in .NET.

    league_id: Indexed(str) = ""  # type: ignore[valid-type]
    manager_user_id: str = ""
    transfer_window_id: str = ""
    players: List[PlayerTeamPosition] = []

    class Settings:
        name = "teams"

    # ------------------------------------------------------------------
    # Composite ID helpers (ported from Team.cs)
    # ------------------------------------------------------------------

    @staticmethod
    def generate_team_id(manager_user_id: str, transfer_window_id: str) -> str:
        return f"{manager_user_id}_{transfer_window_id}"

    def set_composite_id(self) -> None:
        if self.manager_user_id and self.transfer_window_id:
            self.id = self.generate_team_id(self.manager_user_id, self.transfer_window_id)
