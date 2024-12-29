using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace ObsoleteMigrator.Analyzer.Tests;

public class ObsoleteCallDiagnosticAnalyzerTests
{
    private const string SimpleExampleWithObsoleteMethodCall = /*lang=csharp*/ """
            using System;
        
            class Foo
            {
                public void MyMethod()
                {
                    var bar = new Bar();
                    bar.ObsoleteMethod();
                }
            }
        
            class Bar
            {
                [Obsolete]
                public void ObsoleteMethod() { }
            }
        """;

    [Fact(DisplayName = "Analyzer neither throw nor create diagnostics if config file does not exist")]
    public async Task ObsoleteCallsDiagnosticAnalyzer_NoConfigFile_ReportsNoDiagnostics()
    {
        //Arrange
        var test = new AnalyzerTestBuilder<ObsoleteCallDiagnosticAnalyzer>()
            .WithSource(SimpleExampleWithObsoleteMethodCall)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Analyzer neither throw nor create diagnostics if config file is empty")]
    public async Task ObsoleteCallsDiagnosticAnalyzer_EmptyConfigFile_ReportsNoDiagnostics()
    {
        //Arrange
        var test = new AnalyzerTestBuilder<ObsoleteCallDiagnosticAnalyzer>()
            .WithSource(SimpleExampleWithObsoleteMethodCall)
            .WithEmptyConfiguration()
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Analyzer reports diagnostic for obsolete method call with mapping")]
    public async Task ObsoleteCallsDiagnosticAnalyzer_ValidConfigWithMapping_ReportsDiagnostic()
    {
        // Arrange
        var configJson = /*lang=json*/ """
            [{
              "source": {
                "classFullName": "Bar",
                "methodName": "ObsoleteMethod"
              },
              "destination": {
                "classFullName": "NewBar",
                "methodName": "NewMethod"
              },
              "mappings": [
                {
                  "sourceArgument": "param1",
                  "destinationArgument": "newParam"
                }
              ]
            }]
            """;

        var sourceCode = /*lang=csharp*/ """
            class Foo
            {
                public void MyMethod()
                {
                    var bar = new Bar();
                    bar.ObsoleteMethod(123);
                }
            }

            class Bar
            {
                public void ObsoleteMethod(int param1) { }
            }

            class NewBar
            {
                public void NewMethod(int newParam) { }
            }
            """;

        var expectedDiagnostic =
            new DiagnosticResult(ObsoleteCallDiagnosticAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
                .WithLocation(6, 9);

        var test = new AnalyzerTestBuilder<ObsoleteCallDiagnosticAnalyzer>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithExpectedDiagnostics([expectedDiagnostic])
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Analyzer doesn't report diagnostic if config doesn't match the method")]
    public async Task ObsoleteCallsDiagnosticAnalyzer_ConfigDoesntMatchMethod_ReportsNoDiagnostics()
    {
        // Arrange
        var configJson = /*lang=json*/ """
                [{
                  "source": {
                    "classFullName": "Bar",
                    "methodName": "DifferentMethod"
                  },
                  "destination": {
                    "classFullName": "NewBar",
                    "methodName": "NewMethod"
                  },
                    "mappings": []
                }]
            """;

        var sourceCode = /*lang=csharp*/ """
                using System;
            
                class Foo
                {
                    public void MyMethod()
                    {
                        var bar = new Bar();
                        bar.ObsoleteMethod();
                    }
                }
            
                class Bar
                {
                    public void ObsoleteMethod() { }
                }
            
                class NewBar
                {
                    public void NewMethod() { }
                }
            """;

        var test = new AnalyzerTestBuilder<ObsoleteCallDiagnosticAnalyzer>()
            .WithSource(SimpleExampleWithObsoleteMethodCall)
            .WithConfiguration(configJson)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}