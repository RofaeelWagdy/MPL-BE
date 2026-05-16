using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MorkosiaPrepaLeague.Models.Core
{
    /// <summary>
    /// Aggregator document that tracks player ownership counts for a specific transfer window.
    /// Used for optimistic concurrency control when validating team selections.
    /// </summary>
    public class LeagueOwnershipState : CosmosDocumentBase
    {
        [Required]
        public string LeagueId { get; set; } = string.Empty;
        
        [Required]
        public string TransferWindowId { get; set; } = string.Empty;
        
        /// <summary>
        /// Dictionary tracking ownership counts: playerId -> position -> count
        /// Example: { "player1": { "GK": 2, "DEF": 1 }, "player2": { "ATT": 3 } }
        /// </summary>
        public Dictionary<string, Dictionary<string, int>> OwnershipCounts { get; set; } = new();
        
        /// <summary>
        /// Total number of teams that have been submitted for this transfer window
        /// </summary>
        public int TotalTeamsCount { get; set; } = 0;
        
        /// <summary>
        /// Timestamp when this ownership state was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
