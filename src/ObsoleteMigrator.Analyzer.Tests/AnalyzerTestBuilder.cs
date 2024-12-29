using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace ObsoleteMigrator.Analyzer.Tests;

public class AnalyzerTestBuilder<T> where T : DiagnosticAnalyzer, new()
{
    private readonly List<string> _sources = [];
    private readonly List<DiagnosticResult> _expectedDiagnostics = [];
    private readonly Dictionary<string, string> _additionalFiles = new();

    private const string ConfigFilePath = "ObsoleteMigrator.json";

    public AnalyzerTestBuilder<T> WithSource(string source)
    {
        _sources.Add(source);
        return this;
    }

    public AnalyzerTestBuilder<T> WithSources(IEnumerable<string> sources)
    {
        _sources.AddRange(sources);
        return this;
    }

    public AnalyzerTestBuilder<T> WithExpectedDiagnostics(IEnumerable<DiagnosticResult> expectedDiagnostics)
    {
        _expectedDiagnostics.AddRange(expectedDiagnostics);
        return this;
    }

    public AnalyzerTestBuilder<T> WithEmptyConfiguration()
    {
        _additionalFiles.Add(ConfigFilePath, string.Empty);
        return this;
    }

    public AnalyzerTestBuilder<T> WithConfiguration(string configText)
    {
        _additionalFiles.Add(ConfigFilePath, configText);
        return this;
    }

    public CSharpAnalyzerTest<T, DefaultVerifier> Build()
    {
        var test = new CSharpAnalyzerTest<T, DefaultVerifier>();

        // TestState properties are marked as get-only and do not provide AddRange API, so adding separately
        _sources.ForEach(x => test.TestState.Sources.Add(x));
        _expectedDiagnostics.ForEach(x => test.TestState.ExpectedDiagnostics.Add(x));
        _additionalFiles.ToList().ForEach(x => test.TestState.AdditionalFiles.Add((x.Key, x.Value)));

        return test;
    }
}