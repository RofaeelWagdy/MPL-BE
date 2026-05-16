using System.ComponentModel.DataAnnotations;

namespace MorkosiaPrepaLeague.Models.Requests
{
    public class CreateConcreteActivityRequest
    {
        [Required]
        public string TransferWindowId { get; set; } = string.Empty;

        [Required]
        public string ActivityTypeId { get; set; } = string.Empty;

        [Required]
        public DateOnly Date { get; set; }

        public int? OverridePoints { get; set; }
    }

    public class UpdateConcreteActivityRequest
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        [Required]
        public DateOnly Date { get; set; }

        public int? OverridePoints { get; set; }
    }

    public class AddParticipantRequest
    {
        [Required]
        public string ParticipantId { get; set; } = string.Empty;
    }
}
