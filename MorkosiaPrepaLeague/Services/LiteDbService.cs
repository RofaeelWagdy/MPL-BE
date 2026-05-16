using LiteDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MorkosiaPrepaLeague.Models.Core;
using User = MorkosiaPrepaLeague.Models.Core.User;

namespace MorkosiaPrepaLeague.Services
{
    public class LiteDbService : ICosmosDbService, IDisposable
    {
        private readonly LiteDatabase _db;
        private readonly ILogger<LiteDbService> _logger;

        // Collection names mirror Cosmos containers
        private ILiteCollection<League> Leagues => _db.GetCollection<League>("Leagues");
        private ILiteCollection<User> Users => _db.GetCollection<User>("Users");
        private ILiteCollection<TransferWindow> TransferWindows => _db.GetCollection<TransferWindow>("TransferWindows");
        private ILiteCollection<Team> Teams => _db.GetCollection<Team>("Teams");
        private ILiteCollection<LeagueOwnershipState> OwnershipStates => _db.GetCollection<LeagueOwnershipState>("OwnershipStates");
        private ILiteCollection<MplActivityType> ActivityTypes => _db.GetCollection<MplActivityType>("ActivityTypes");
        private ILiteCollection<ConcreteActivity> ConcreteActivities => _db.GetCollection<ConcreteActivity>("ConcreteActivities");
        private ILiteCollection<AttendanceRequest> AttendanceRequests => _db.GetCollection<AttendanceRequest>("AttendanceRequests");

        public LiteDbService(IConfiguration configuration, ILogger<LiteDbService> logger)
        {
            _logger = logger;
            var dbPath = configuration["LiteDb:Path"] ?? "mpl.db";

            // Ensure the directory exists (important when path includes a folder)
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            _db = new LiteDatabase(dbPath);

            // Create indexes for frequent query fields
            Leagues.EnsureIndex(x => x.Id);
            Users.EnsureIndex(x => x.Id);
            Users.EnsureIndex("$.username", false);
            TransferWindows.EnsureIndex(x => x.Id);
            TransferWindows.EnsureIndex("$.leagueId", false);
            Teams.EnsureIndex(x => x.Id);
            Teams.EnsureIndex("$.leagueId", false);
            ActivityTypes.EnsureIndex("$.leagueId", false);
            ConcreteActivities.EnsureIndex("$.transferWindowId", false);
            AttendanceRequests.EnsureIndex("$.leagueId", false);

            _logger.LogInformation("LiteDbService initialized. Database path: {DbPath}", dbPath);
        }

        // ── Leagues ──────────────────────────────────────────────────────────

        public Task<League?> AddLeagueAsync(League league)
        {
            if (string.IsNullOrEmpty(league.Id)) league.Id = Guid.NewGuid().ToString();
            league.Ver = 1;

            if (Leagues.FindById(league.Id) != null)
            {
                _logger.LogWarning("League with ID {LeagueId} already exists.", league.Id);
                return Task.FromResult<League?>(null);
            }

            Leagues.Insert(league);
            _logger.LogInformation("Created league with ID: {LeagueId}", league.Id);
            return Task.FromResult<League?>(league);
        }

        public Task<bool> LeagueNameExistsAsync(string name)
        {
            var exists = Leagues.Exists(x => x.Name.ToLower() == name.ToLower());
            return Task.FromResult(exists);
        }

        public Task<League?> GetLeagueByIdAsync(string id)
        {
            var league = Leagues.FindById(id);
            return Task.FromResult<League?>(league);
        }

        public Task<IEnumerable<League>> GetLeaguesByIdsAsync(IEnumerable<string> ids)
        {
            var idSet = ids.ToHashSet();
            var results = Leagues.Find(x => idSet.Contains(x.Id));
            return Task.FromResult<IEnumerable<League>>(results.ToList());
        }

        public Task<IEnumerable<League>> GetAllLeaguesAsync()
        {
            var results = Leagues.FindAll().ToList();
            _logger.LogInformation("Retrieved {Count} leagues", results.Count);
            return Task.FromResult<IEnumerable<League>>(results);
        }

        public Task<bool> UpdateLeagueAsync(League league)
        {
            var updated = Leagues.Update(league);
            if (!updated) _logger.LogWarning("League {LeagueId} not found for update.", league.Id);
            return Task.FromResult(updated);
        }

        // ── Users ─────────────────────────────────────────────────────────────

        public Task<User?> AddUserAsync(User user)
        {
            if (string.IsNullOrEmpty(user.Id)) user.Id = Guid.NewGuid().ToString();
            user.Ver = 1;

            if (Users.FindById(user.Id) != null)
            {
                _logger.LogWarning("User with ID {UserId} already exists.", user.Id);
                return Task.FromResult<User?>(null);
            }

            Users.Insert(user);
            _logger.LogInformation("Created user with ID: {UserId}", user.Id);
            return Task.FromResult<User?>(user);
        }

        public Task<bool> UsernameExistsAsync(string username)
        {
            var exists = Users.Exists(x => x.Username.ToLower() == username.ToLower());
            return Task.FromResult(exists);
        }

        public Task<User?> GetUserByIdAsync(string id)
        {
            var user = Users.FindById(id);
            return Task.FromResult<User?>(user);
        }

        public Task<User?> GetUserByUsernameAsync(string username)
        {
            var user = Users.FindOne(x => x.Username.ToLower() == username.ToLower());
            return Task.FromResult<User?>(user);
        }

        public Task<User?> UpdateUserAsync(User user)
        {
            var updated = Users.Update(user);
            if (!updated)
            {
                _logger.LogWarning("User {UserId} not found for update.", user.Id);
                return Task.FromResult<User?>(null);
            }
            return Task.FromResult<User?>(user);
        }

        public Task<IEnumerable<User>> GetAllUsersForLeaguesAsync(IEnumerable<string> leagueIds)
        {
            var idSet = leagueIds.ToHashSet();
            var users = Users.FindAll()
                .Where(u =>
                    (u.LeaguesAdmin != null && u.LeaguesAdmin.Any(l => idSet.Contains(l))) ||
                    (u.LeaguesMember != null && u.LeaguesMember.Any(l => idSet.Contains(l))))
                .ToList();

            _logger.LogInformation("Retrieved {Count} users for specified leagues", users.Count);
            return Task.FromResult<IEnumerable<User>>(users);
        }

        // ── Transfer Windows ──────────────────────────────────────────────────

        public Task<IEnumerable<TransferWindow>> GetAllTransferWindowsAsync(string leagueId)
        {
            var results = TransferWindows
                .Find(x => x.LeagueId == leagueId)
                .OrderBy(x => x.StartDate)
                .ToList();
            return Task.FromResult<IEnumerable<TransferWindow>>(results);
        }

        public Task<TransferWindow?> GetCurrentTransferWindowAsync(string leagueId)
        {
            var now = DateTime.UtcNow;
            var window = TransferWindows
                .Find(x => x.LeagueId == leagueId && x.StartDate <= now)
                .OrderByDescending(x => x.StartDate)
                .FirstOrDefault();
            return Task.FromResult<TransferWindow?>(window);
        }

        public Task<TransferWindow?> GetTransferWindowByIdAsync(string leagueId, string windowId)
        {
            var window = TransferWindows.FindOne(x => x.Id == windowId && x.LeagueId == leagueId);
            return Task.FromResult<TransferWindow?>(window);
        }

        public async Task<TransferWindow?> CreateTransferWindowAsync(TransferWindow transferWindow)
        {
            if (string.IsNullOrEmpty(transferWindow.Id)) transferWindow.Id = Guid.NewGuid().ToString();

            var existing = await GetAllTransferWindowsAsync(transferWindow.LeagueId);
            transferWindow.WindowNumber = existing.Count() + 1;
            transferWindow.Ver = 1;

            TransferWindows.Insert(transferWindow);
            _logger.LogInformation("Created transfer window {WindowId} for league {LeagueId}", transferWindow.Id, transferWindow.LeagueId);

            try
            {
                await InitializeLeagueOwnershipStateAsync(transferWindow.LeagueId, transferWindow.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize ownership state for transfer window {WindowId}", transferWindow.Id);
            }

            return transferWindow;
        }

        // ── Teams ─────────────────────────────────────────────────────────────

        public Task<IEnumerable<Team>> GetAllTeamsInLeagueAsync(string leagueId)
        {
            var teams = Teams.Find(x => x.LeagueId == leagueId).ToList();
            return Task.FromResult<IEnumerable<Team>>(teams);
        }

        public Task<IEnumerable<Team>> GetTeamsForTransferWindowAsync(string leagueId, string transferWindowId)
        {
            var teams = Teams
                .Find(x => x.LeagueId == leagueId && x.TransferWindowId == transferWindowId)
                .ToList();
            return Task.FromResult<IEnumerable<Team>>(teams);
        }

        public Task<Team?> GetManagerTeamForTransferWindowAsync(string leagueId, string transferWindowId, string managerUserId)
        {
            var teamId = Team.GenerateTeamId(managerUserId, transferWindowId);
            var team = Teams.FindOne(x => x.Id == teamId && x.LeagueId == leagueId);
            return Task.FromResult<Team?>(team);
        }

        public Task<Team?> SaveTeamAsync(Team team)
        {
            team.SetCompositeId();
            if (string.IsNullOrEmpty(team.Id))
                throw new InvalidOperationException("Unable to generate team ID.");

            // Upsert
            if (Teams.FindById(team.Id) != null)
                Teams.Update(team);
            else
                Teams.Insert(team);

            return Task.FromResult<Team?>(team);
        }

        public async Task<Dictionary<string, int>> GetPlayerOwnershipCountsAsync(string leagueId)
        {
            var currentWindow = await GetCurrentTransferWindowAsync(leagueId);
            if (currentWindow == null) return new Dictionary<string, int>();

            var teams = await GetTeamsForTransferWindowAsync(leagueId, currentWindow.Id!);
            var counts = new Dictionary<string, int>();

            foreach (var team in teams)
                foreach (var player in team.Players)
                    counts[player.PlayerId] = counts.GetValueOrDefault(player.PlayerId) + 1;

            return counts;
        }

        public async Task<Dictionary<string, Dictionary<string, int>>> GetPlayerPositionOwnershipCountsAsync(string leagueId)
        {
            var currentWindow = await GetCurrentTransferWindowAsync(leagueId);
            if (currentWindow == null) return new Dictionary<string, Dictionary<string, int>>();

            var teams = await GetTeamsForTransferWindowAsync(leagueId, currentWindow.Id!);
            var counts = new Dictionary<string, Dictionary<string, int>>();

            foreach (var team in teams)
            {
                foreach (var player in team.Players)
                {
                    if (!counts.ContainsKey(player.PlayerId))
                        counts[player.PlayerId] = new Dictionary<string, int>();

                    counts[player.PlayerId][player.Position] =
                        counts[player.PlayerId].GetValueOrDefault(player.Position) + 1;
                }
            }

            return counts;
        }

        // ── Optimistic Concurrency (simplified for local use) ─────────────────

        public Task<(LeagueOwnershipState?, string?)> GetLeagueOwnershipStateWithETagAsync(string leagueId, string transferWindowId)
        {
            var ownershipStateId = $"ownership-{transferWindowId}";
            var state = OwnershipStates.FindOne(x => x.Id == ownershipStateId && x.LeagueId == leagueId);

            // Use a hash of LastUpdated as a lightweight ETag substitute
            var etag = state != null ? state.LastUpdated.Ticks.ToString() : null;
            return Task.FromResult<(LeagueOwnershipState?, string?)>((state, etag));
        }

        public Task<LeagueOwnershipState?> GetLeagueOwnershipStateAsync(string leagueId, string transferWindowId) =>
            GetLeagueOwnershipStateWithETagAsync(leagueId, transferWindowId)
                .ContinueWith(t => t.Result.Item1);

        public Task<LeagueOwnershipState> InitializeLeagueOwnershipStateAsync(string leagueId, string transferWindowId)
        {
            var ownershipStateId = $"ownership-{transferWindowId}";
            var existing = OwnershipStates.FindOne(x => x.Id == ownershipStateId);
            if (existing != null) return Task.FromResult(existing);

            var state = new LeagueOwnershipState
            {
                Id = ownershipStateId,
                LeagueId = leagueId,
                TransferWindowId = transferWindowId,
                OwnershipCounts = new Dictionary<string, Dictionary<string, int>>(),
                TotalTeamsCount = 0,
                LastUpdated = DateTime.UtcNow
            };

            OwnershipStates.Insert(state);
            return Task.FromResult(state);
        }

        public async Task<bool> SaveTeamWithOwnershipUpdateAsync(Team team, LeagueOwnershipState ownershipState, string expectedETag)
        {
            // Validate ETag (optimistic concurrency check)
            var (current, currentETag) = await GetLeagueOwnershipStateWithETagAsync(ownershipState.LeagueId, ownershipState.TransferWindowId);
            if (current != null && currentETag != expectedETag)
            {
                _logger.LogInformation("Optimistic concurrency conflict for team {TeamId}", team.Id);
                return false;
            }

            team.SetCompositeId();

            // Update players in ownership state
            foreach (var player in team.Players)
            {
                if (!ownershipState.OwnershipCounts.ContainsKey(player.PlayerId))
                    ownershipState.OwnershipCounts[player.PlayerId] = new Dictionary<string, int>();

                ownershipState.OwnershipCounts[player.PlayerId][player.Position] =
                    ownershipState.OwnershipCounts[player.PlayerId].GetValueOrDefault(player.Position) + 1;
            }

            ownershipState.TotalTeamsCount++;
            ownershipState.LastUpdated = DateTime.UtcNow;

            if (Teams.FindById(team.Id) != null) Teams.Update(team); else Teams.Insert(team);
            OwnershipStates.Update(ownershipState);

            return true;
        }

        // ── Activity Types ────────────────────────────────────────────────────

        public Task<MplActivityType?> AddActivityTypeAsync(MplActivityType activityType)
        {
            if (string.IsNullOrEmpty(activityType.Id)) activityType.Id = Guid.NewGuid().ToString();
            activityType.Ver = 1;

            if (ActivityTypes.FindById(activityType.Id) != null)
                return Task.FromResult<MplActivityType?>(null);

            ActivityTypes.Insert(activityType);
            return Task.FromResult<MplActivityType?>(activityType);
        }

        public Task<MplActivityType?> GetActivityTypeByIdAsync(string id)
        {
            var result = ActivityTypes.FindById(id);
            return Task.FromResult<MplActivityType?>(result);
        }

        public Task<MplActivityType?> UpdateActivityTypeAsync(MplActivityType activityType)
        {
            var updated = ActivityTypes.Update(activityType);
            return Task.FromResult<MplActivityType?>(updated ? activityType : null);
        }

        public Task<IEnumerable<MplActivityType>> GetActivityTypesForLeagueAsync(string leagueId)
        {
            var results = ActivityTypes.Find(x => x.LeagueId == leagueId).ToList();
            return Task.FromResult<IEnumerable<MplActivityType>>(results);
        }

        // ── Concrete Activities ───────────────────────────────────────────────

        public Task<ConcreteActivity?> AddConcreteActivityAsync(ConcreteActivity concreteActivity)
        {
            if (string.IsNullOrEmpty(concreteActivity.Id)) concreteActivity.Id = Guid.NewGuid().ToString();
            concreteActivity.Ver = 1;

            if (ConcreteActivities.FindById(concreteActivity.Id) != null)
                return Task.FromResult<ConcreteActivity?>(null);

            ConcreteActivities.Insert(concreteActivity);
            return Task.FromResult<ConcreteActivity?>(concreteActivity);
        }

        public Task<ConcreteActivity?> GetConcreteActivityByIdAsync(string id, string transferWindowId)
        {
            var result = ConcreteActivities.FindOne(x => x.Id == id && x.TransferWindowId == transferWindowId);
            return Task.FromResult<ConcreteActivity?>(result);
        }

        public Task<ConcreteActivity?> UpdateConcreteActivityAsync(ConcreteActivity concreteActivity)
        {
            var updated = ConcreteActivities.Update(concreteActivity);
            return Task.FromResult<ConcreteActivity?>(updated ? concreteActivity : null);
        }

        public Task<bool> DeleteConcreteActivityAsync(string id, string transferWindowId)
        {
            var activity = ConcreteActivities.FindOne(x => x.Id == id && x.TransferWindowId == transferWindowId);
            if (activity == null) return Task.FromResult(false);
            return Task.FromResult(ConcreteActivities.Delete(id));
        }

        public Task<IEnumerable<ConcreteActivity>> GetConcreteActivitiesForTransferWindowAsync(
            string transferWindowId, DateOnly? from = null, DateOnly? to = null, string? activityTypeId = null)
        {
            var results = ConcreteActivities
                .Find(x => x.TransferWindowId == transferWindowId)
                .Where(x =>
                    (from == null || x.Date >= from.Value) &&
                    (to == null || x.Date <= to.Value) &&
                    (activityTypeId == null || x.ActivityTypeId == activityTypeId))
                .OrderByDescending(x => x.Date)
                .ToList();

            return Task.FromResult<IEnumerable<ConcreteActivity>>(results);
        }

        public async Task<bool> AddParticipantToConcreteActivityAsync(string activityId, string transferWindowId, string participantId)
        {
            var activity = await GetConcreteActivityByIdAsync(activityId, transferWindowId);
            if (activity == null) return false;
            if (activity.ParticipantIds.Contains(participantId)) return true;

            activity.ParticipantIds.Add(participantId);
            activity.UpdatedAt = DateTime.UtcNow;
            var result = await UpdateConcreteActivityAsync(activity);
            return result != null;
        }

        public async Task<bool> RemoveParticipantFromConcreteActivityAsync(string activityId, string transferWindowId, string participantId)
        {
            var activity = await GetConcreteActivityByIdAsync(activityId, transferWindowId);
            if (activity == null) return false;
            if (!activity.ParticipantIds.Contains(participantId)) return true;

            activity.ParticipantIds.Remove(participantId);
            activity.UpdatedAt = DateTime.UtcNow;
            var result = await UpdateConcreteActivityAsync(activity);
            return result != null;
        }

        // ── Attendance Requests ───────────────────────────────────────────────

        public Task<AttendanceRequest?> CreateAttendanceRequestAsync(AttendanceRequest request)
        {
            if (string.IsNullOrEmpty(request.Id)) request.Id = Guid.NewGuid().ToString();
            request.Ver = 1;

            if (AttendanceRequests.FindById(request.Id) != null)
                return Task.FromResult<AttendanceRequest?>(null);

            AttendanceRequests.Insert(request);
            return Task.FromResult<AttendanceRequest?>(request);
        }

        public Task<AttendanceRequest?> GetAttendanceRequestByIdAsync(string id)
        {
            var result = AttendanceRequests.FindById(id);
            return Task.FromResult<AttendanceRequest?>(result);
        }

        public Task<AttendanceRequest?> UpdateAttendanceRequestAsync(AttendanceRequest request)
        {
            var updated = AttendanceRequests.Update(request);
            return Task.FromResult<AttendanceRequest?>(updated ? request : null);
        }

        public Task<IEnumerable<AttendanceRequest>> GetPendingAttendanceRequestsForLeagueAsync(string leagueId)
        {
            var results = AttendanceRequests
                .Find(x => x.LeagueId == leagueId && x.Status == 0)
                .OrderByDescending(x => x.RequestedAt)
                .ToList();
            return Task.FromResult<IEnumerable<AttendanceRequest>>(results);
        }

        public Task<IEnumerable<AttendanceRequest>> GetAttendanceRequestsForStudentAsync(string leagueId, string studentUserId)
        {
            var results = AttendanceRequests
                .Find(x => x.LeagueId == leagueId && x.StudentUserId == studentUserId)
                .OrderByDescending(x => x.RequestedAt)
                .ToList();
            return Task.FromResult<IEnumerable<AttendanceRequest>>(results);
        }

        public void Dispose() => _db.Dispose();
    }
}
