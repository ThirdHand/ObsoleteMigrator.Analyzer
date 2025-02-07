using LightJson;

namespace ObsoleteMigrator.Analyzer.Configuration.Models;

internal class MigrationMapping
{
    public required string SourceArgument { get; init; }
    public required string DestinationArgument { get; init; }

    public static MigrationMapping FromJson(JsonValue json)
    {
        return new MigrationMapping
        {
            SourceArgument = json["sourceArgument"].AsString,
            DestinationArgument = json["destinationArgument"].AsString
        };
    }
}