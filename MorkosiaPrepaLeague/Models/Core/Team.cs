using System.ComponentModel.DataAnnotations;

namespace MorkosiaPrepaLeague.Models.Core
{
    public class Team : CosmosDocumentBase
    {
        [Required]
        public string LeagueId { get; set; } = string.Empty;

        [Required]
        public string ManagerUserId { get; set; } = string.Empty;

        [Required]
        public string TransferWindowId { get; set; } = string.Empty;

        public List<PlayerTeamPosition> Players { get; set; } = new List<PlayerTeamPosition>();

        /// <summary>
        /// Generates a deterministic ID based on manager and transfer window to ensure one team per manager per window
        /// </summary>
        /// <param name="managerUserId">The manager's user ID</param>
        /// <param name="transferWindowId">The transfer window ID</param>
        /// <returns>A composite ID for the team</returns>
        public static string GenerateTeamId(string managerUserId, string transferWindowId)
        {
            return $"{managerUserId}_{transferWindowId}";
        }

        /// <summary>
        /// Sets the team ID using the composite key approach
        /// </summary>
        public void SetCompositeId()
        {
            if (!string.IsNullOrEmpty(ManagerUserId) && !string.IsNullOrEmpty(TransferWindowId))
            {
                Id = GenerateTeamId(ManagerUserId, TransferWindowId);
            }
        }
    }

    public class PlayerTeamPosition
    {
        public string PlayerId { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
    }
}
