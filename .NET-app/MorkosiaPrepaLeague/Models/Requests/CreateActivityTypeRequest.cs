using System.ComponentModel.DataAnnotations;

namespace MorkosiaPrepaLeague.Models.Requests
{
    public record CreateActivityTypeRequest(
        [Required] string Name,
        [Range(0, int.MaxValue, ErrorMessage = "DefaultPoints must be non-negative")] int DefaultPoints,
        [Required] string LeagueId,
        List<string> LinkedPositions
    );
}
