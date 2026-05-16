namespace MorkosiaPrepaLeague.Models.Core
{
    public class MplActivityType : CosmosDocumentBase
    {
        public string Name { get; set; } = string.Empty;

        public int DefaultPoints { get; set; }

        public string LeagueId { get; set; } = string.Empty;

        public List<string> LinkedPositions { get; set; } = new();
    }
}
