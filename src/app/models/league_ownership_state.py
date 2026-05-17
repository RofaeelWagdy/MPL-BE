from datetime import datetime, timezone
from typing import Dict

from pydantic import Field

from app.models.base import DocumentBase


class LeagueOwnershipState(DocumentBase):
    
    """
    Stored in the `teams` collection (alongside Team documents, same as in .NET).
    Mirrors LeagueOwnershipState.cs.

    Tracks how many teams each player is selected on per position,
    for a specific transfer window. Used for optimistic concurrency control
    when managers submit their team picks simultaneously.

    `id` format: "ownership-{transfer_window_id}"
    `ownership_counts` format: { player_id: { position: count } }
    """

    league_id: str = ""
    transfer_window_id: str = ""
    ownership_counts: Dict[str, Dict[str, int]] = {}
    total_teams_count: int = 0
    last_updated: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))

    class Settings:
        name = "teams"  # Lives in the same collection as Team documents
