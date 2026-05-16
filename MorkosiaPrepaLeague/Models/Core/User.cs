using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace MorkosiaPrepaLeague.Models.Core
{
    public class User : CosmosDocumentBase
    {
        public string Username { get; set; } = string.Empty;
        
        public string FullName { get; set; } = string.Empty;
        
        public string Class { get; set; } = string.Empty;
        
        public IList<string> LeaguesAdmin { get; set; } = new List<string>();
        
        public IList<string> LeaguesMember { get; set; } = new List<string>();

        [JsonIgnore]
        public string HashedPassword { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}