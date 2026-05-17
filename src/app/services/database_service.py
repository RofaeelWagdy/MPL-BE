import re
from datetime import date, datetime, timezone
from typing import Optional
from uuid import uuid4

from fastapi import HTTPException, status

from app.models.activity_type import ActivityType
from app.models.attendance_request import AttendanceRequest, AttendanceRequestStatus
from app.models.concrete_activity import ConcreteActivity
from app.models.league import League
from app.models.league_ownership_state import LeagueOwnershipState
from app.models.team import Team
from app.models.transfer_window import TransferWindow
from app.models.user import User


class DatabaseService:
    """
    The single service class for all database operations.
    Translates CosmosDbService.cs + ICosmosDbService.cs into async Beanie queries.

    No constructor arguments needed — Beanie manages the MongoDB connection globally
    via the `init_beanie()` call in database.py (equivalent to .NET's singleton DI).
    """

    # ------------------------------------------------------------------
    # Leagues
    # ------------------------------------------------------------------

    async def add_league(self, league: League) -> League:
        if not league.id:
            league.id = str(uuid4())
        league.ver = 1
        await league.insert()
        return league

    async def league_name_exists(self, name: str) -> bool:
        existing = await League.find_one(
            {"name": {"$regex": f"^{re.escape(name)}$", "$options": "i"}}
        )
        return existing is not None

    async def get_league_by_id(self, league_id: str) -> Optional[League]:
        return await League.get(league_id)

    async def get_leagues_by_ids(self, ids: list[str]) -> list[League]:
        return await League.find({"id": {"$in": ids}}).to_list()

    async def get_all_leagues(self) -> list[League]:
        return await League.find_all().to_list()

    async def update_league(self, league: League) -> bool:
        existing = await League.get(league.id)
        if existing is None:
            return False
        await league.save()
        return True

    # ------------------------------------------------------------------
    # Users
    # ------------------------------------------------------------------

    async def add_user(self, user: User) -> User:
        if not user.id:
            user.id = str(uuid4())
        user.ver = 1
        await user.insert()
        return user

    async def username_exists(self, username: str) -> bool:
        existing = await User.find_one(
            {"username": {"$regex": f"^{re.escape(username)}$", "$options": "i"}}
        )
        return existing is not None

    async def get_user_by_id(self, user_id: str) -> Optional[User]:
        return await User.get(user_id)

    async def get_user_by_username(self, username: str) -> Optional[User]:
        return await User.find_one(
            {"username": {"$regex": f"^{re.escape(username)}$", "$options": "i"}}
        )

    async def get_all_users_for_leagues(self, league_ids: list[str]) -> list[User]:
        if not league_ids:
            return []
        return await User.find({
            "$or": [
                {"leagues_admin": {"$in": league_ids}},
                {"leagues_member": {"$in": league_ids}},
            ]
        }).to_list()

    async def update_user(self, user: User) -> Optional[User]:
        existing = await User.get(user.id)
        if existing is None:
            return None
        await user.save()
        return user

    # ------------------------------------------------------------------
    # Transfer Windows
    # ------------------------------------------------------------------

    async def get_all_transfer_windows(self, league_id: str) -> list[TransferWindow]:
        return (
            await TransferWindow.find(TransferWindow.league_id == league_id)
            .sort(TransferWindow.start_date)
            .to_list()
        )

    async def get_current_transfer_window(self, league_id: str) -> Optional[TransferWindow]:
        """Returns the most recently started window (start_date <= now)."""
        now = datetime.now(timezone.utc)
        return await (
            TransferWindow.find(
                TransferWindow.league_id == league_id,
                TransferWindow.start_date <= now,
            )
            .sort(-TransferWindow.start_date)
            .first_or_none()
        )

    async def get_transfer_window_by_id(
        self, league_id: str, window_id: str
    ) -> Optional[TransferWindow]:
        return await TransferWindow.find_one(
            TransferWindow.id == window_id,
            TransferWindow.league_id == league_id,
        )

    async def create_transfer_window(self, transfer_window: TransferWindow) -> TransferWindow:
        if not transfer_window.id:
            transfer_window.id = str(uuid4())

        existing = await self.get_all_transfer_windows(transfer_window.league_id)
        transfer_window.window_number = len(existing) + 1
        transfer_window.ver = 1

        await transfer_window.insert()

        # Initialize ownership state for this new window (non-fatal if it fails)
        try:
            await self.initialize_league_ownership_state(
                transfer_window.league_id, transfer_window.id
            )
        except Exception:
            pass

        return transfer_window

    # ------------------------------------------------------------------
    # Teams
    # ------------------------------------------------------------------

    async def get_all_teams_in_league(self, league_id: str) -> list[Team]:
        return await Team.find(Team.league_id == league_id).to_list()

    async def get_teams_for_transfer_window(
        self, league_id: str, transfer_window_id: str
    ) -> list[Team]:
        return await Team.find(
            Team.league_id == league_id,
            Team.transfer_window_id == transfer_window_id,
        ).to_list()

    async def get_manager_team_for_transfer_window(
        self, league_id: str, transfer_window_id: str, manager_user_id: str
    ) -> Optional[Team]:
        team_id = Team.generate_team_id(manager_user_id, transfer_window_id)
        return await Team.find_one(Team.id == team_id, Team.league_id == league_id)

    async def save_team(self, team: Team) -> Team:
        team.set_composite_id()
        existing = await Team.get(team.id)
        if existing:
            existing.players = team.players
            await existing.save()
            return existing
        await team.insert()
        return team

    async def get_player_ownership_counts(self, league_id: str) -> dict[str, int]:
        current_window = await self.get_current_transfer_window(league_id)
        if current_window is None:
            return {}

        teams = await self.get_teams_for_transfer_window(league_id, current_window.id)
        counts: dict[str, int] = {}
        for team in teams:
            for player in team.players:
                counts[player.player_id] = counts.get(player.player_id, 0) + 1
        return counts

    async def get_player_position_ownership_counts(
        self, league_id: str
    ) -> dict[str, dict[str, int]]:
        current_window = await self.get_current_transfer_window(league_id)
        if current_window is None:
            return {}

        teams = await self.get_teams_for_transfer_window(league_id, current_window.id)
        counts: dict[str, dict[str, int]] = {}
        for team in teams:
            for player in team.players:
                if player.player_id not in counts:
                    counts[player.player_id] = {}
                pos = player.position
                counts[player.player_id][pos] = counts[player.player_id].get(pos, 0) + 1
        return counts

    # ------------------------------------------------------------------
    # Activity Types
    # ------------------------------------------------------------------

    async def add_activity_type(self, activity_type: ActivityType) -> ActivityType:
        if not activity_type.id:
            activity_type.id = str(uuid4())
        activity_type.ver = 1
        await activity_type.insert()
        return activity_type

    async def get_activity_type_by_id(self, activity_type_id: str) -> Optional[ActivityType]:
        return await ActivityType.get(activity_type_id)

    async def update_activity_type(
        self, activity_type: ActivityType
    ) -> Optional[ActivityType]:
        existing = await ActivityType.get(activity_type.id)
        if existing is None:
            return None
        await activity_type.save()
        return activity_type

    async def get_activity_types_for_league(self, league_id: str) -> list[ActivityType]:
        return await ActivityType.find(ActivityType.league_id == league_id).to_list()

    # ------------------------------------------------------------------
    # Concrete Activities
    # ------------------------------------------------------------------

    async def add_concrete_activity(
        self, concrete_activity: ConcreteActivity
    ) -> ConcreteActivity:
        if not concrete_activity.id:
            concrete_activity.id = str(uuid4())
        concrete_activity.ver = 1
        await concrete_activity.insert()
        return concrete_activity

    async def get_concrete_activity_by_id(
        self, activity_id: str, transfer_window_id: str
    ) -> Optional[ConcreteActivity]:
        return await ConcreteActivity.find_one(
            ConcreteActivity.id == activity_id,
            ConcreteActivity.transfer_window_id == transfer_window_id,
        )

    async def update_concrete_activity(
        self, concrete_activity: ConcreteActivity
    ) -> Optional[ConcreteActivity]:
        existing = await self.get_concrete_activity_by_id(
            concrete_activity.id, concrete_activity.transfer_window_id
        )
        if existing is None:
            return None
        await concrete_activity.save()
        return concrete_activity

    async def delete_concrete_activity(
        self, activity_id: str, transfer_window_id: str
    ) -> bool:
        activity = await self.get_concrete_activity_by_id(activity_id, transfer_window_id)
        if activity is None:
            return False
        await activity.delete()
        return True

    async def get_concrete_activities_for_transfer_window(
        self,
        transfer_window_id: str,
        from_date: Optional[date] = None,
        to_date: Optional[date] = None,
        activity_type_id: Optional[str] = None,
    ) -> list[ConcreteActivity]:
        filters: dict = {"transfer_window_id": transfer_window_id}

        if from_date:
            filters.setdefault("date", {})["$gte"] = from_date.isoformat()
        if to_date:
            filters.setdefault("date", {})["$lte"] = to_date.isoformat()
        if activity_type_id:
            filters["activity_type_id"] = activity_type_id

        return (
            await ConcreteActivity.find(filters)
            .sort(-ConcreteActivity.date)
            .to_list()
        )

    async def add_participant_to_concrete_activity(
        self, activity_id: str, transfer_window_id: str, participant_id: str
    ) -> bool:
        activity = await self.get_concrete_activity_by_id(activity_id, transfer_window_id)
        if activity is None:
            return False
        if participant_id in activity.participant_ids:
            return True  # already present — idempotent
        activity.participant_ids.append(participant_id)
        activity.updated_at = datetime.now(timezone.utc)
        await activity.save()
        return True

    async def remove_participant_from_concrete_activity(
        self, activity_id: str, transfer_window_id: str, participant_id: str
    ) -> bool:
        activity = await self.get_concrete_activity_by_id(activity_id, transfer_window_id)
        if activity is None:
            return False
        if participant_id not in activity.participant_ids:
            return True  # already absent — idempotent
        activity.participant_ids.remove(participant_id)
        activity.updated_at = datetime.now(timezone.utc)
        await activity.save()
        return True

    # ------------------------------------------------------------------
    # Attendance Requests
    # ------------------------------------------------------------------

    async def create_attendance_request(
        self, request: AttendanceRequest
    ) -> AttendanceRequest:
        if not request.id:
            request.id = str(uuid4())
        request.ver = 1
        await request.insert()
        return request

    async def get_attendance_request_by_id(
        self, request_id: str
    ) -> Optional[AttendanceRequest]:
        return await AttendanceRequest.get(request_id)

    async def update_attendance_request(
        self, request: AttendanceRequest
    ) -> Optional[AttendanceRequest]:
        existing = await AttendanceRequest.get(request.id)
        if existing is None:
            return None
        await request.save()
        return request

    async def get_pending_attendance_requests_for_league(
        self, league_id: str
    ) -> list[AttendanceRequest]:
        return (
            await AttendanceRequest.find(
                AttendanceRequest.league_id == league_id,
                AttendanceRequest.status == AttendanceRequestStatus.PENDING,
            )
            .sort(-AttendanceRequest.requested_at)
            .to_list()
        )

    async def get_attendance_requests_for_student(
        self, league_id: str, student_user_id: str
    ) -> list[AttendanceRequest]:
        return (
            await AttendanceRequest.find(
                AttendanceRequest.league_id == league_id,
                AttendanceRequest.student_user_id == student_user_id,
            )
            .sort(-AttendanceRequest.requested_at)
            .to_list()
        )

    # ------------------------------------------------------------------
    # Optimistic Concurrency (LeagueOwnershipState)
    #
    # MongoDB has no native ETag like CosmosDB.
    # We simulate it using `last_updated` as a string ETag.
    # If two requests read the same timestamp and both try to write,
    # only the first one proceeds — the second sees a mismatch and returns False.
    # ------------------------------------------------------------------

    async def get_league_ownership_state_with_etag(
        self, league_id: str, transfer_window_id: str
    ) -> tuple[Optional[LeagueOwnershipState], Optional[str]]:
        ownership_id = f"ownership-{transfer_window_id}"
        state = await LeagueOwnershipState.find_one(
            LeagueOwnershipState.id == ownership_id,
            LeagueOwnershipState.league_id == league_id,
        )
        if state is None:
            return None, None
        etag = str(state.last_updated.timestamp())
        return state, etag

    async def initialize_league_ownership_state(
        self, league_id: str, transfer_window_id: str
    ) -> LeagueOwnershipState:
        ownership_id = f"ownership-{transfer_window_id}"

        # Return existing if already initialized
        existing = await LeagueOwnershipState.find_one(
            LeagueOwnershipState.id == ownership_id
        )
        if existing:
            return existing

        state = LeagueOwnershipState(
            id=ownership_id,
            league_id=league_id,
            transfer_window_id=transfer_window_id,
            ownership_counts={},
            total_teams_count=0,
            last_updated=datetime.now(timezone.utc),
        )
        await state.insert()
        return state

    async def save_team_with_ownership_update(
        self,
        team: Team,
        ownership_state: LeagueOwnershipState,
        expected_etag: str,
    ) -> bool:
        """
        Atomically saves a team and updates the ownership counts.

        The ETag check acts as optimistic concurrency control:
        if another request has already updated the ownership state
        since this request read it, the ETags will not match and
        this method returns False — the caller then retries.
        """
        # Verify the ownership state hasn't been modified since we read it
        current_state, current_etag = await self.get_league_ownership_state_with_etag(
            ownership_state.league_id, ownership_state.transfer_window_id
        )
        if current_state is not None and current_etag != expected_etag:
            return False  # Concurrency conflict — caller should retry

        # Update ownership counts with the new team's players
        for player in team.players:
            if player.player_id not in ownership_state.ownership_counts:
                ownership_state.ownership_counts[player.player_id] = {}
            pos_counts = ownership_state.ownership_counts[player.player_id]
            pos_counts[player.position] = pos_counts.get(player.position, 0) + 1

        ownership_state.total_teams_count += 1
        ownership_state.last_updated = datetime.now(timezone.utc)

        # Save the team (upsert by composite id)
        team.set_composite_id()
        existing_team = await Team.get(team.id)
        if existing_team:
            existing_team.players = team.players
            await existing_team.save()
        else:
            await team.insert()

        # Save the updated ownership state
        await ownership_state.save()
        return True
