namespace MorkosiaPrepaLeague.Models.Core
{
    public class TeamScoresResponse
    {
        public List<TeamScore> Teams { get; set; } = new();
    }

    public class TeamScore
    {
        public string TeamId { get; set; } = string.Empty;
        public string TransferWindowId { get; set; } = string.Empty;
        public string ManagerUserId { get; set; } = string.Empty;
        public string ManagerName { get; set; } = string.Empty;
        public int TotalScore { get; set; }
        public int WeekScore { get; set; }
        public List<PlayerScore> Players { get; set; } = new();
    }

    public class PlayerScore
    {
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public List<PlayerActivityScore> Activities { get; set; } = new();
    }

    public class PlayerActivityScore
    {
        public string ActivityId { get; set; } = string.Empty;
        public string ActivityName { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public int Points { get; set; }
        public int PotentialPoints { get; set; }
        public bool Qualifies { get; set; }
        public bool Participated { get; set; }
    }
}
