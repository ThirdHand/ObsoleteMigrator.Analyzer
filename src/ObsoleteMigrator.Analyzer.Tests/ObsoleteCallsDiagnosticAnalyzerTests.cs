using System.Threading.Tasks;
using Xunit;

namespace ObsoleteMigrator.Analyzer.Tests;

public class ObsoleteCallsDiagnosticAnalyzerTests
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
        var test = new AnalyzerTestBuilder<ObsoleteCallsDiagnosticAnalyzer>()
            .WithSource(SimpleExampleWithObsoleteMethodCall)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Analyzer neither throw nor create diagnostics if config file is empty")]
    public async Task ObsoleteCallsDiagnosticAnalyzer_EmptyConfigFile_ReportsNoDiagnostics()
    {
        //Arrange
        var test = new AnalyzerTestBuilder<ObsoleteCallsDiagnosticAnalyzer>()
            .WithSource(SimpleExampleWithObsoleteMethodCall)
            .WithEmptyConfiguration()
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}