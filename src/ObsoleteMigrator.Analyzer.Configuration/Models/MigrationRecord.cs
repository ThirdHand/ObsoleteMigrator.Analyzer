using System.Text.Json.Serialization;

namespace ObsoleteMigrator.Analyzer.Configuration.Models
{
    public class MigrationRecord
    {
        [JsonPropertyName("source")]
        public MigrationStatement Source { get; set; } = null!;

        [JsonPropertyName("destination")]
        public MigrationStatement Destination { get; set; } = null!;

        [JsonPropertyName("mappings")]
        public MigrationMapping[] Mappings { get; set; } = null!;
    }
}