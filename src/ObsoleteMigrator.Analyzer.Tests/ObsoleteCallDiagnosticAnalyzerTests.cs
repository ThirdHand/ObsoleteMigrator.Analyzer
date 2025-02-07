using System.Threading.Tasks;
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

    [Fact(DisplayName = "Анализатор не создает диагностику, если конфигурационный файл отсутствует")]
    public async Task ObsoleteCallsDiagnosticAnalyzer_NoConfigFile_ReportsNoDiagnostics()
    {
        //Arrange
        var test = new AnalyzerTestBuilder<ObsoleteCallDiagnosticAnalyzer>()
            .WithSource(SimpleExampleWithObsoleteMethodCall)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Анализатор не создает диагностику, если конфигурационный файл пустой")]
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

    [Fact(DisplayName = "Анализатор репортит диагностику, если конфигурация валидна")]
    public async Task ObsoleteCallsDiagnosticAnalyzer_ValidConfigWithMapping_ReportsDiagnostic()
    {
        // Arrange
        const string configJson = /*lang=json*/ """
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

        const string sourceCode = /*lang=csharp*/ """
            class Foo
            {
                public void MyMethod()
                {
                    var bar = new Bar();
                    [|bar.ObsoleteMethod(123)|];
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

        var test = new AnalyzerTestBuilder<ObsoleteCallDiagnosticAnalyzer>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Анализатор не создает диагностику для методов не из конфига")]
    public async Task ObsoleteCallsDiagnosticAnalyzer_ConfigDoesntMatchMethod_ReportsNoDiagnostics()
    {
        // Arrange
        const string configJson = /*lang=json*/ """
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

        var test = new AnalyzerTestBuilder<ObsoleteCallDiagnosticAnalyzer>()
            .WithSource(SimpleExampleWithObsoleteMethodCall)
            .WithConfiguration(configJson)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}