using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using ObsoleteMigrator.Analyzer.Configuration.Models;
using ObsoleteMigrator.Analyzer.Models;

namespace ObsoleteMigrator.Analyzer.Configuration;

internal static class MigratorConfigurationProvider
{
    private static Dictionary<MappingKey, MigrationRecord>? _migrationRecords;
    private static ImmutableArray<byte>? _configHash;

    public static void Initialize(SourceText? configSourceText)
    {
        if (configSourceText == null || configSourceText.Length == 0)
        {
            _migrationRecords = null;
            _configHash = null;

            return;
        }

        var newConfigHash = configSourceText.GetContentHash();
        if (_configHash?.SequenceEqual(newConfigHash) ?? false)
        {
            return;
        }

        _configHash = newConfigHash;

        _migrationRecords = MigratorConfigurationJsonParser.Parse(configSourceText.ToString())
            .ToDictionary(x => new MappingKey(
                x.Source.ClassFullName,
                x.Source.MethodName));
    }

    public static MigrationRecord? Get(MappingKey mappingKey)
    {
        if (_migrationRecords == null)
        {
            return null;
        }

        return _migrationRecords.TryGetValue(mappingKey, out var migrationRecord)
            ? migrationRecord
            : null;
    }
}