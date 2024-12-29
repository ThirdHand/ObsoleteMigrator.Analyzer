using System.Text.Json;
using ObsoleteMigrator.Analyzer.Configuration.Json;

namespace ObsoleteMigrator.Analyzer.Configuration
{
    public class MigratorConfiguration
    {
        private readonly Dictionary<(string, string), MigrationRecord> _migrationRecords;

        private MigratorConfiguration(Dictionary<(string, string), MigrationRecord> migrationRecords)
        {
            _migrationRecords = migrationRecords;
        }

        public static MigratorConfiguration? CreateFromJson(string? jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                return null;
            }

            var migrationRecords = JsonSerializer.Deserialize<MigrationRecord[]>(jsonText)!;

            var migrationRecordsMap = migrationRecords
                .ToDictionary(x => (x.Source.ClassFullName, x.Source.MethodName));

            return new MigratorConfiguration(migrationRecordsMap);
        }

        public MigrationRecord? GetMigrationRecord(string classFullName, string methodName)
        {
            return _migrationRecords.TryGetValue((classFullName, methodName), out var record)
                ? record
                : null;
        }
    }
}