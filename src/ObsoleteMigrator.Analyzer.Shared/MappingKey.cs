namespace ObsoleteMigrator.Analyzer.Shared
{
    public readonly record struct MappingKey(string DisplayType, string MethodName);
}

// This is small hack to make compiler happy about using record struct with netstandard2.0
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}