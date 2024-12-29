using System.Text.Json.Serialization;

namespace ObsoleteMigrator.Analyzer.Configuration.Models
{
    public class MigrationStatement
    {
        [JsonPropertyName("classFullName")]
        public string ClassFullName { get; set; } = null!;

        [JsonPropertyName("methodName")]
        public string MethodName { get; set; } = null!;
    }
}