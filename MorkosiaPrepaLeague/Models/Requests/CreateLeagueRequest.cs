using System.ComponentModel.DataAnnotations;

namespace MorkosiaPrepaLeague.Models.Requests
{
    public class CreateLeagueRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [RegularExpression(
            "^(ActivityPoints|H2H)$",
            ErrorMessage = "Type must be 'ActivityPoints' or 'H2H'"
        )]
        public string Type { get; set; } = string.Empty;
    }
}
