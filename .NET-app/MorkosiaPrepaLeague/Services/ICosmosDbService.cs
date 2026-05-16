using MorkosiaPrepaLeague.Models.Core;

namespace MorkosiaPrepaLeague.Services
{
    public interface ICosmosDbService
    {
        Task<League?> AddLeagueAsync(League league);
        Task<bool> LeagueNameExistsAsync(string name);
        Task<League?> GetLeagueByIdAsync(string id);
        Task<IEnumerable<League>> GetLeaguesByIdsAsync(IEnumerable<string> ids);
        Task<IEnumerable<League>> GetAllLeaguesAsync();
        Task<bool> UpdateLeagueAsync(League league);

        Task<User?> AddUserAsync(User user);
        Task<bool> UsernameExistsAsync(string username);
        Task<User?> GetUserByIdAsync(string id);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<IEnumerable<User>> GetAllUsersForLeaguesAsync(IEnumerable<string> leagueIds);

        Task<User?> UpdateUserAsync(User user);

        Task<TransferWindow?> GetCurrentTransferWindowAsync(string leagueId);
        Task<IEnumerable<TransferWindow>> GetAllTransferWindowsAsync(string leagueId);
        Task<TransferWindow?> GetTransferWindowByIdAsync(string leagueId, string windowId);
        Task<TransferWindow?> CreateTransferWindowAsync(TransferWindow transferWindow);

        Task<IEnumerable<Team>> GetAllTeamsInLeagueAsync(string leagueId);
        Task<IEnumerable<Team>> GetTeamsForTransferWindowAsync(
            string leagueId,
            string transferWindowId
        );
        Task<Team?> GetManagerTeamForTransferWindowAsync(
            string leagueId,
            string transferWindowId,
            string managerUserId
        );
        Task<Team?> SaveTeamAsync(Team team);
        Task<Dictionary<string, int>> GetPlayerOwnershipCountsAsync(string leagueId);
        Task<Dictionary<string, Dictionary<string, int>>> GetPlayerPositionOwnershipCountsAsync(
            string leagueId
        );

        // Activity Type methods
        Task<MplActivityType?> AddActivityTypeAsync(MplActivityType activityType);
        Task<MplActivityType?> GetActivityTypeByIdAsync(string id);
        Task<MplActivityType?> UpdateActivityTypeAsync(MplActivityType activityType);
        Task<IEnumerable<MplActivityType>> GetActivityTypesForLeagueAsync(string leagueId);

        // Concrete Activity methods
        Task<ConcreteActivity?> AddConcreteActivityAsync(ConcreteActivity concreteActivity);
        Task<ConcreteActivity?> GetConcreteActivityByIdAsync(string id, string transferWindowId);
        Task<ConcreteActivity?> UpdateConcreteActivityAsync(ConcreteActivity concreteActivity);
        Task<bool> DeleteConcreteActivityAsync(string id, string transferWindowId);
        Task<IEnumerable<ConcreteActivity>> GetConcreteActivitiesForTransferWindowAsync(
            string transferWindowId,
            DateOnly? from = null,
            DateOnly? to = null,
            string? activityTypeId = null
        );
        Task<bool> AddParticipantToConcreteActivityAsync(
            string activityId,
            string transferWindowId,
            string participantId
        );
        Task<bool> RemoveParticipantFromConcreteActivityAsync(
            string activityId,
            string transferWindowId,
            string participantId
        );

        // Attendance Request methods
        Task<AttendanceRequest?> CreateAttendanceRequestAsync(AttendanceRequest request);
        Task<AttendanceRequest?> GetAttendanceRequestByIdAsync(string id);
        Task<AttendanceRequest?> UpdateAttendanceRequestAsync(AttendanceRequest request);
        Task<IEnumerable<AttendanceRequest>> GetPendingAttendanceRequestsForLeagueAsync(
            string leagueId
        );
        Task<IEnumerable<AttendanceRequest>> GetAttendanceRequestsForStudentAsync(
            string leagueId,
            string studentUserId
        );

        // Optimistic Concurrency Control methods
        Task<(LeagueOwnershipState?, string?)> GetLeagueOwnershipStateWithETagAsync(
            string leagueId,
            string transferWindowId
        );
        Task<LeagueOwnershipState> InitializeLeagueOwnershipStateAsync(
            string leagueId,
            string transferWindowId
        );
        Task<bool> SaveTeamWithOwnershipUpdateAsync(
            Team team,
            LeagueOwnershipState ownershipState,
            string expectedETag
        );
    }
}
