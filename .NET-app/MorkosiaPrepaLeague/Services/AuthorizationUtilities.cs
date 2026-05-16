using System.Security.Claims;
using System.Linq;
using MorkosiaPrepaLeague.Models.Core;

namespace MorkosiaPrepaLeague.Services
{
    public static class AuthorizationUtilities
    {
        private static bool IsSuperAdmin(ClaimsPrincipal user)
        {
            var currentUserRole = user?.FindFirst(ClaimTypes.Role)?.Value;
            return currentUserRole == Role.SuperAdmin.ToString();
        }

        private static IList<string> GetUserAdminLeagues(ClaimsPrincipal user)
        {
            return user?.FindAll("AdminLeague").Select(c => c.Value).ToList() ?? new List<string>();
        }

        private static IList<string> GetUserMemberLeagues(ClaimsPrincipal user)
        {
            return user?.FindAll("MemberLeague").Select(c => c.Value).ToList() ?? new List<string>();
        }

        private static bool IsLeagueAdmin(ClaimsPrincipal user)
        {
            var currentUserRole = user?.FindFirst(ClaimTypes.Role)?.Value;
            return currentUserRole == Role.LeagueAdmin.ToString();
        }

        public static bool IsUserAdminForLeague(ClaimsPrincipal user, string leagueId)
        {
            if (user == null || string.IsNullOrWhiteSpace(leagueId))
            {
                return false;
            }

            if (IsSuperAdmin(user))
            {
                return true;
            }

            var adminLeagues = GetUserAdminLeagues(user);
            return adminLeagues.Contains(leagueId);
        }

        public static IEnumerable<string> GetAdminAccessibleLeagueIds(ClaimsPrincipal user, IEnumerable<string>? allLeagueIds = null)
        {
            if (user == null)
            {
                return Enumerable.Empty<string>();
            }

            if (IsSuperAdmin(user))
            {
                return allLeagueIds ?? Enumerable.Empty<string>();
            }

            return GetUserAdminLeagues(user);
        }

        public static bool CanUserManageTargetUser(ClaimsPrincipal currentUser, string? currentUserId, string targetUserId, IList<string>? targetUserLeaguesMember)
        {
            if (currentUser == null || string.IsNullOrWhiteSpace(targetUserId))
            {
                return false;
            }

            if (IsSuperAdmin(currentUser) || currentUserId == targetUserId)
            {
                return true;
            }

            if (!IsLeagueAdmin(currentUser))
            {
                return false;
            }

            var adminLeagues = GetUserAdminLeagues(currentUser);
            var targetUserLeagues = targetUserLeaguesMember ?? new List<string>();
            return adminLeagues.Any(adminLeague => targetUserLeagues.Contains(adminLeague));
        }

        public static bool CanUserPickTeamFromLeague(ClaimsPrincipal user, string leagueId)
        {
            if (user == null || string.IsNullOrWhiteSpace(leagueId))
            {
                return false;
            }

            // Check if user is SuperAdmin or Admin - they cannot pick teams
            if (IsSuperAdmin(user) || IsLeagueAdmin(user))
            {
                return false;
            }

            // Check if user is a member of the specified league
            var userMemberLeagues = GetUserMemberLeagues(user);
            return userMemberLeagues.Contains(leagueId);
        }
    }
}
