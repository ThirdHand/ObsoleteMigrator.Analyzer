using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using ObsoleteMigrator.Analyzer.Shared;
using Xunit.Internal;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;

namespace ObsoleteMigrator.Analyzer.Tests;

public class CodeFixTestBuilder<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    private readonly List<string> _sources = [];
    private readonly Dictionary<string, string> _additionalFiles = new();
    private string? _fixedCode;
    private readonly List<DiagnosticResult> _expectedDiagnostics = [];

    public CodeFixTestBuilder<TAnalyzer, TCodeFix> WithSource(string source)
    {
        _sources.Add(source);
        return this;
    }

    public CodeFixTestBuilder<TAnalyzer, TCodeFix> WithConfiguration(string configText)
    {
        _additionalFiles[MigratorConstants.ConfigurationFilePath] = configText;
        return this;
    }

    public CodeFixTestBuilder<TAnalyzer, TCodeFix> WithFixedCode(string fixedCode)
    {
        _fixedCode = fixedCode;
        return this;
    }

    public CodeFixTestBuilder<TAnalyzer, TCodeFix> WithExpectedDiagnostics(
        params DiagnosticResult[] expectedDiagnostics)
    {
        _expectedDiagnostics.AddRange(expectedDiagnostics);
        return this;
    }

    public CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier> Build()
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>();

        _sources.ForEach(x => test.TestState.Sources.Add(x));
        _additionalFiles.ForEach(kvp => test.TestState.AdditionalFiles.Add((kvp.Key, kvp.Value)));
        _expectedDiagnostics.ForEach(x => test.TestState.ExpectedDiagnostics.Add(x));

        if (_fixedCode != null)
        {
            test.FixedState.Sources.Add(_fixedCode);
            test.FixedState.MarkupHandling = MarkupMode.Allow;
        }

        return test;
    }
}