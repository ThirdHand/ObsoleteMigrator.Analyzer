using System.Text.Json.Serialization;

namespace ObsoleteMigrator.Analyzer.Configuration.Models
{
    public class MigrationMapping
    {
        [JsonPropertyName("sourceArgument")]
        public string SourceArgument { get; set; } = null!;

        [JsonPropertyName("destinationArgument")]
        public string DestinationArgument { get; set; } = null!;
    }
}