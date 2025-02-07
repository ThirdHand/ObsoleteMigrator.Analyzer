using System.Linq;
using LightJson;

namespace ObsoleteMigrator.Analyzer.Configuration.Models;

internal class MigrationRecord
{
    public required MigrationStatement Source { get; init; }

    public required MigrationStatement Destination { get; init; }

    public required MigrationMapping[] Mappings { get; init; }

    public static MigrationRecord FromJson(JsonValue json)
    {
        return new MigrationRecord
        {
            Source = MigrationStatement.FromJson(json["source"]),
            Destination = MigrationStatement.FromJson(json["destination"]),
            Mappings = json["mappings"].AsJsonArray
                .Select(MigrationMapping.FromJson)
                .ToArray()
        };
    }
}