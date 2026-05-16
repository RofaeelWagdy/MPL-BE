namespace MorkosiaPrepaLeague.Models.Core
{
    public class ConcreteActivity : CosmosDocumentBase
    {
        public string DocumentType { get; } = "ConcreteActivity";

        public string TransferWindowId { get; set; } = string.Empty;

        public string ActivityTypeId { get; set; } = string.Empty;

        public DateOnly Date { get; set; }

        public int? OverridePoints { get; set; }

        public List<string> ParticipantIds { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
