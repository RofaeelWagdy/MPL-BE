using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace MorkosiaPrepaLeague.Models.Core
{
    public class League : CosmosDocumentBase
    {
        [Required]
        [Range(3, 50, ErrorMessage = "League name length must be between 1 and 50")]
        public string Name { get; set; } = default!;

        [Required]
        [RegularExpression("^(ActivityPoints|H2H)$", ErrorMessage = "Type must be 'ActivityPoints' or 'H2H'")]
        public string Type { get; set; } = default!;

        [Range(0, int.MaxValue, ErrorMessage = "Initial budget must be a non-negative value")]
        public int? InitialBudget { get; set; }

        /// <summary>
        /// Team structure: Collection of positions and how many players are needed for each position
        /// </summary>
        public List<TeamPosition>? TeamPositions { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Default player price must be a non-negative value")]
        public int? DefaultPlayerPrice { get; set; }

        /// <summary>
        /// Player-specific price overrides: Key = UserId, Value = price for that player in this league.
        /// </summary>
        public Dictionary<string, int>? PlayerPriceOverrides { get; set; }

        /// <summary>
        /// Team/Manager-specific budget overrides: Key = ManagerUserId, Value = custom budget for that manager's team in this league.
        /// If a manager is not in this dictionary, they use the InitialBudget value.
        /// </summary>
        public Dictionary<string, int>? BudgetOverrides { get; set; }

        /// <summary>
        /// Default ownership cap for any member-position combination that doesn't have a specific override.
        /// If null, no default cap is enforced.
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Default ownership cap must be a non-negative value")]
        public int? DefaultOwnershipCap { get; set; }

        /// <summary>
        /// Member-specific position-based ownership caps. 
        /// Key = MemberUserId, Value = Dictionary where Key = Position name, Value = Max teams for that member-position combination.
        /// Example: { "user123": { "GK": 1, "DEF": 2 }, "user456": { "MID": 3 } }
        /// </summary>
        public Dictionary<string, Dictionary<string, int>>? MemberPositionOwnershipCaps { get; set; }

        /// <summary>
        /// Calculates the total team size based on the sum of all position counts.
        /// </summary>
        /// <returns>The total team size or null if positions are not defined.</returns>
        public int? GetTotalTeamSize()
        {
            if (TeamPositions != null && TeamPositions.Any())
            {
                return TeamPositions.Sum(p => p.Count);
            }
            
            return null;
        }

        /// <summary>
        /// Gets the effective budget for a specific manager, considering budget overrides.
        /// </summary>
        /// <param name="managerUserId">The user ID of the manager</param>
        /// <returns>The budget amount for the manager, or null if no budget is configured</returns>
        public int? GetEffectiveBudget(string managerUserId)
        {
            if (string.IsNullOrEmpty(managerUserId))
                return InitialBudget;

            // Check if there's a specific budget override for this manager
            if (BudgetOverrides?.ContainsKey(managerUserId) == true)
                return BudgetOverrides[managerUserId];

            // Fall back to the initial budget
            return InitialBudget;
        }

        /// <summary>
        /// Gets the effective ownership cap for a specific member-position combination.
        /// </summary>
        /// <param name="memberUserId">The user ID of the member</param>
        /// <param name="positionName">The name of the position</param>
        /// <returns>The ownership cap for the member-position combination, or null if no cap is configured</returns>
        public int? GetEffectiveOwnershipCap(string memberUserId, string positionName)
        {
            if (string.IsNullOrEmpty(memberUserId) || string.IsNullOrEmpty(positionName))
                return DefaultOwnershipCap;

            // Check if there's a specific cap for this member-position combination
            if (MemberPositionOwnershipCaps?.ContainsKey(memberUserId) == true &&
                MemberPositionOwnershipCaps[memberUserId]?.ContainsKey(positionName) == true)
            {
                return MemberPositionOwnershipCaps[memberUserId][positionName];
            }

            // Fall back to the default ownership cap
            return DefaultOwnershipCap;
        }

        /// <summary>
        /// Gets the price for a specific player in this league, considering overrides and defaults.
        /// </summary>
        /// <param name="playerId">The player's user ID</param>
        /// <returns>The price for the player</returns>
        public int GetPlayerPrice(string playerId)
        {
            // Check if there's a specific price override for this player
            if (PlayerPriceOverrides?.ContainsKey(playerId) == true)
            {
                return PlayerPriceOverrides[playerId];
            }

            // Fall back to the default player price, or 0 if not set
            return DefaultPlayerPrice ?? 0;
        }
    }

    public class TeamPosition
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Count must be at least 1")]
        public int Count { get; set; }
    }
}
