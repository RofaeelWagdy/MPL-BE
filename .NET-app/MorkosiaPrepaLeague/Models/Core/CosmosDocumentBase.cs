using System.Text.Json.Serialization;

namespace MorkosiaPrepaLeague.Models.Core
{
    public abstract class CosmosDocumentBase
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // used internally in case we need to update the schema for a model
        // since Cosmos DB does not support schema migrations, we can use this to track versions
        public int Ver { get; set; } = 1;
    }
}
