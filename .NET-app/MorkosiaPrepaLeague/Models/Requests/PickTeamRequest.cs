using System.ComponentModel.DataAnnotations;

namespace MorkosiaPrepaLeague.Models.Requests
{
    /// <summary>
    /// Request model for picking a team in a league's transfer window
    /// </summary>
    public class PickTeamRequest
    {
        /// <summary>
        /// The ID of the league this team belongs to
        /// </summary>
        [Required]
        public string LeagueId { get; set; } = string.Empty;

        /// <summary>
        /// The list of players selected for the team with their positions
        /// </summary>
        [Required]
        public List<PlayerSelectionDto> Players { get; set; } = new List<PlayerSelectionDto>();
    }

    /// <summary>
    /// Represents a player selection with their designated position
    /// </summary>
    public class PlayerSelectionDto
    {
        /// <summary>
        /// The ID of the selected player (User ID)
        /// </summary>
        [Required]
        public string PlayerId { get; set; } = string.Empty;

        /// <summary>
        /// The position assigned to this player in the team
        /// </summary>
        [Required]
        public string Position { get; set; } = string.Empty;
    }
}
