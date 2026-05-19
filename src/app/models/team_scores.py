from typing import List

from pydantic import BaseModel


# These are pure response types — not stored in MongoDB.
# Mirrors TeamScoresResponse.cs, TeamScore.cs, PlayerScore.cs, PlayerActivityScore.cs.
# They live here rather than in schemas/ because they represent read-only
# computed data assembled from multiple collections, not incoming request payloads.


class PlayerActivityScore(BaseModel):
    activity_id: str = ""
    activity_name: str = ""
    transfer_window_id: str =""
    date: str = ""
    points: int = 0
    potential_points: int = 0
    qualifies: bool = False
    participated: bool = False


class PlayerScore(BaseModel):
    player_id: str = ""
    player_name: str = ""
    position: str = ""
    activities: List[PlayerActivityScore] = []


class TeamScore(BaseModel):
    team_id: str = ""
    transfer_window_id: str = ""
    manager_user_id: str = ""
    manager_name: str = ""
    total_score: int = 0
    week_score: int = 0
    players: List[PlayerScore] = []


class TeamScoresResponse(BaseModel):
    teams: List[TeamScore] = []
