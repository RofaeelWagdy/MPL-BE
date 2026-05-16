using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MorkosiaPrepaLeague.Models.Core;

namespace MorkosiaPrepaLeague.Models.Requests
{
    public class ActivityPointsLeagueConfigUpdateDto
    {
        [Range(0, int.MaxValue, ErrorMessage = "Initial budget must be a non-negative value")]
        public int? InitialBudget { get; set; }

        /// <summary>
        /// Defines team structure as a collection of positions and how many players are needed for each position.
        /// Providing a new list will overwrite the existing one.
        /// </summary>
        public List<TeamPosition>? TeamPositions { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Default player price must be a non-negative value")]
        public int? DefaultPlayerPrice { get; set; }

        /// <summary>
        /// Specific price overrides for players in this league. Key: UserId, Value: Price.
        /// Providing a new dictionary will overwrite the existing one.
        /// </summary>
        public Dictionary<string, int>? PlayerPriceOverrides { get; set; }

        /// <summary>
        /// Team/Manager-specific budget overrides. Key: ManagerUserId, Value: Custom budget.
        /// Providing a new dictionary will overwrite the existing one.
        /// </summary>
        public Dictionary<string, int>? BudgetOverrides { get; set; }

        /// <summary>
        /// Default ownership cap for any member-position combination that doesn't have a specific override.
        /// If null, no default cap will be enforced.
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Default ownership cap must be a non-negative value")]
        public int? DefaultOwnershipCap { get; set; }

        /// <summary>
        /// Member-specific position-based ownership caps.
        /// Key = MemberUserId, Value = Dictionary where Key = Position name, Value = Max teams for that member-position combination.
        /// Providing a new dictionary will overwrite the existing one.
        /// Example: { "user123": { "GK": 1, "DEF": 2 }, "user456": { "MID": 3 } }
        /// </summary>
        public Dictionary<string, Dictionary<string, int>>? MemberPositionOwnershipCaps { get; set; }
    }
}