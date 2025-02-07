using LightJson;

namespace ObsoleteMigrator.Analyzer.Configuration.Models;

internal class MigrationStatement
{
    public required string ClassFullName { get; init; }

    public required string MethodName { get; init; }

    public static MigrationStatement FromJson(JsonValue json)
    {
        return new MigrationStatement
        {
            ClassFullName = json["classFullName"].AsString,
            MethodName = json["methodName"].AsString
        };
    }
}