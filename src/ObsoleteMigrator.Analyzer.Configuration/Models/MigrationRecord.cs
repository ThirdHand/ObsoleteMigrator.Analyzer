namespace ObsoleteMigrator.Analyzer.Configuration.Json
{
    public class MigrationRecord
    {
        public MigrationStatement Source { get; }

        public MigrationStatement Destination { get; }

        public MigrationMapping[] Mappings { get; }
    }
}