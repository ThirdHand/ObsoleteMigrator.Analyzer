using System.Text.Json;
using ObsoleteMigrator.Analyzer.Configuration.Models;
using ObsoleteMigrator.Analyzer.Shared;

namespace ObsoleteMigrator.Analyzer.Configuration
{
    public static class MigratorConfiguration
    {


        private static Dictionary<MappingKey, MigrationRecord> _migrationRecords = null!;

        public static bool TryInitialize(string? migratorConfigurationJsonText)
        {
            if (string.IsNullOrWhiteSpace(migratorConfigurationJsonText))
            {
                return false;
            }

            var migrationRecords = JsonSerializer.Deserialize<MigrationRecord[]>(migratorConfigurationJsonText!)!;

            _migrationRecords = migrationRecords
                .ToDictionary(x => new MappingKey(x.Source.ClassFullName, x.Source.MethodName));

            return true;
        }

        public static MigrationRecord GetMigrationRecord(MappingKey mappingKey)
        {
            return _migrationRecords[mappingKey];
        }

        public static bool ContainsMigrationRecord(MappingKey mappingKey)
        {
            return _migrationRecords.ContainsKey(mappingKey);
        }
    }
}