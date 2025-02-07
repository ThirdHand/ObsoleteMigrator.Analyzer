using System.Collections.Generic;
using System.Linq;
using LightJson;
using ObsoleteMigrator.Analyzer.Configuration.Models;

namespace ObsoleteMigrator.Analyzer.Configuration;

internal static class MigratorConfigurationJsonParser
{
    public static IEnumerable<MigrationRecord> Parse(string? configurationText)
    {
        if (string.IsNullOrWhiteSpace(configurationText))
        {
            return [];
        }

        var jsonValue = JsonValue.Parse(configurationText);

        var migrationRecords = jsonValue.AsJsonArray
            .Select(MigrationRecord.FromJson)
            .ToArray();

        return migrationRecords;
    }
}