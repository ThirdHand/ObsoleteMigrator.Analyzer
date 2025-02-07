namespace ObsoleteMigrator.Analyzer;

public static class MigratorConstants
{
    public const string DiagnosticId = "OCD0001";
    public const string ConfigurationFilePath = "ObsoleteMigrator.json";

    public const string DiagnosticTitle = "Устаревший вызов метода";
    public const string CodeFixTitle = "Заменить устаревший вызов";
    public const string Category = "Modernization";
    public const string MessageFormat = "Метод '{0}' устарел. Используйте '{1}' вместо текущей реализации.";
}