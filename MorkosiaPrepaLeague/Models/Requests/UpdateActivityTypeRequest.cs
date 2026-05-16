using System.ComponentModel.DataAnnotations;

namespace MorkosiaPrepaLeague.Models.Requests
{
    public record UpdateActivityTypeRequest(
        [property: Required] string Name,
        [property: Range(0, int.MaxValue, ErrorMessage = "DefaultPoints must be non-negative")] int DefaultPoints,
        List<string> LinkedPositions
    );
}
