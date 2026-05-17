from typing import Dict, List, Optional

from pydantic import BaseModel, Field

from app.models.base import DocumentBase


class TeamPosition(BaseModel):
    """A single position slot in a team (e.g. name='GK', count=1)."""
    name: str
    count: int = Field(ge=1)


class League(DocumentBase):
    name: str
    type: str  # "ActivityPoints" or "H2H"
    initial_budget: Optional[int] = None
    team_positions: Optional[List[TeamPosition]] = None
    default_player_price: Optional[int] = None

    # Key: player user_id → custom price
    player_price_overrides: Optional[Dict[str, int]] = None

    # Key: manager user_id → custom budget
    budget_overrides: Optional[Dict[str, int]] = None

    # Cap applied when no member-specific cap exists for a position
    default_ownership_cap: Optional[int] = None

    # Key: member user_id → { position_name: max_teams }
    member_position_ownership_caps: Optional[Dict[str, Dict[str, int]]] = None

    class Settings:
        name = "leagues"

    # ------------------------------------------------------------------
    # Business logic helpers (ported directly from League.cs methods)
    # ------------------------------------------------------------------

    def get_total_team_size(self) -> Optional[int]:
        if self.team_positions:
            return sum(p.count for p in self.team_positions)
        return None

    def get_effective_budget(self, manager_user_id: str) -> Optional[int]:
        if self.budget_overrides and manager_user_id in self.budget_overrides:
            return self.budget_overrides[manager_user_id]
        return self.initial_budget

    def get_effective_ownership_cap(
        self, member_user_id: str, position_name: str
    ) -> Optional[int]:
        if self.member_position_ownership_caps:
            member_caps = self.member_position_ownership_caps.get(member_user_id)
            if member_caps and position_name in member_caps:
                return member_caps[position_name]
        return self.default_ownership_cap

    def get_player_price(self, player_id: str) -> int:
        if self.player_price_overrides and player_id in self.player_price_overrides:
            return self.player_price_overrides[player_id]
        return self.default_player_price or 0
