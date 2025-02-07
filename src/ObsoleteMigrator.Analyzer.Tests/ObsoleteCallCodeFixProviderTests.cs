using System.Threading.Tasks;
using Xunit;

namespace ObsoleteMigrator.Analyzer.Tests;

public class ObsoleteCallCodeFixProviderTests
{
    private const string ParameterlessValidReplacementConfigJson = /*lang=json*/ """
        [{
            "source": {
                "classFullName": "Bar",
                "methodName": "OldMethod"
            },
            "destination": {
                "classFullName": "External.NewBar",
                "methodName": "NewMethod"
            },
            "mappings": []
        }]
        """;

    [Fact(DisplayName = "Код-фикс заменяет вызов, создает поле, конструктор и using при необходимости")]
    public async Task ObsoleteCallCodeFixProvider_ValidConfigNoCtorNoFieldNoUsing_CreatesFieldAndConstructorAndUsing()
    {
        // Arrange
        const string sourceCode = /*lang=csharp*/ """
            using System;

            class Foo
            {
                private readonly int _test;

                public void Test()
                {
                    [|new Bar().OldMethod()|];
                }
            }

            class Bar { public void OldMethod() { } }
            namespace External { public class NewBar { public void NewMethod() { } } }
            """;

        const string fixedCode = /*lang=csharp*/ """
            using System;
            using External;

            class Foo
            {
                private readonly int _test;
                private readonly NewBar _newBar;
            
                public Foo(NewBar newBar)
                {
                    _newBar = newBar;
                }
            
                public void Test()
                {
                    _newBar.NewMethod();
                }
            }

            class Bar { public void OldMethod() { } }
            namespace External { public class NewBar { public void NewMethod() { } } }
            """;

        var test = new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallCodeFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(ParameterlessValidReplacementConfigJson)
            .WithFixedCode(fixedCode)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Код-фикс использует существующее поле класса из destination")]
    public async Task ObsoleteCallCodeFixProvider_DestinationFieldExists_UsesExistingField()
    {
        // Arrange
        const string sourceCode = /*lang=csharp*/ """
            using System;
            using External;

            class Foo
            {
                private readonly NewBar _newBar;
            
                public Foo(NewBar nb) => _newBar = nb;
            
                public void Test() => [|new Bar().OldMethod()|];
            }

            class Bar { public void OldMethod() { } }
            namespace External { public class NewBar { public void NewMethod() { } } }
            """;

        const string fixedCode = /*lang=csharp*/ """
            using System;
            using External;

            class Foo
            {
                private readonly NewBar _newBar;

                public Foo(NewBar nb) => _newBar = nb;

                public void Test() => _newBar.NewMethod();
            }

            class Bar { public void OldMethod() { } }
            namespace External { public class NewBar { public void NewMethod() { } } }
            """;

        var test = new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallCodeFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(ParameterlessValidReplacementConfigJson)
            .WithFixedCode(fixedCode)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Код-фикс добавляет недостающее поле и инжектит его в существующем конструкторе")]
    public async Task ObsoleteCallCodeFixProvider_NoDestFieldButCtorExists_AddsFieldAndInjectsIt()
    {
        // Arrange
        const string sourceCode = /*lang=csharp*/ """
            using System;
            using External;

            class Foo
            {
                public Foo()
                {
                }

                public void Test() => [|new Bar().OldMethod()|];
            }

            class Bar { public void OldMethod() { } }
            namespace External { public class NewBar { public void NewMethod() { } } }
            """;

        const string fixedCode = /*lang=csharp*/ """
            using System;
            using External;

            class Foo
            {
                private readonly NewBar _newBar;
            
                public Foo(NewBar newBar)
                {
                    _newBar = newBar;
                }
            
                public void Test() => _newBar.NewMethod();
            }

            class Bar { public void OldMethod() { } }
            namespace External { public class NewBar { public void NewMethod() { } } }
            """;

        var test = new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallCodeFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(ParameterlessValidReplacementConfigJson)
            .WithFixedCode(fixedCode)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Код-фикс добавляет недостающее поле и добавляет конструктор с инжектом")]
    public async Task ObsoleteCallCodeFixProvider_NoDestFieldNoCtor_AddsFieldAndInjectsItInNewCtor()
    {
        // Arrange
        const string sourceCode = /*lang=csharp*/ """
            using System;
            using External;

            class Foo
            {
                public void Test() => [|new Bar().OldMethod()|];
            }

            class Bar { public void OldMethod() { } }
            namespace External { public class NewBar { public void NewMethod() { } } }
            """;

        const string fixedCode = /*lang=csharp*/ """
            using System;
            using External;

            class Foo
            {
                private readonly NewBar _newBar;
            
                public Foo(NewBar newBar)
                {
                    _newBar = newBar;
                }
            
                public void Test() => _newBar.NewMethod();
            }

            class Bar { public void OldMethod() { } }
            namespace External { public class NewBar { public void NewMethod() { } } }
            """;

        var test = new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallCodeFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(ParameterlessValidReplacementConfigJson)
            .WithFixedCode(fixedCode)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Код-фикс правильно обрабатывает ref параметры")]
    public async Task ObsoleteCallCodeFixProvider_RefParameterExists_HandlesRefParameters()
    {
        // Arrange
        const string configJson = /*lang=json*/ """
            [{
                "source": {
                    "classFullName": "Calculator",
                    "methodName": "Compute"
                },
                "destination": {
                    "classFullName": "NewCalculator",
                    "methodName": "Calculate"
                },
                "mappings": [{
                    "sourceArgument": "x",
                    "destinationArgument": "input"
                }]
            }]
            """;

        const string sourceCode = /*lang=csharp*/ """
            class Program
            {
                private readonly NewCalculator _newCalc;

                public Program(NewCalculator nc) => _newCalc = nc;

                void Run()
                {
                    int x = 5;
                    [|new Calculator().Compute(ref x)|];
                }
            }

            class Calculator { public void Compute(ref int x) { } }
            class NewCalculator { public void Calculate(ref int input) { } }
            """;

        const string fixedCode = /*lang=csharp*/ """
            class Program
            {
                private readonly NewCalculator _newCalc;
            
                public Program(NewCalculator nc) => _newCalc = nc;
            
                void Run()
                {
                    int x = 5;
                    _newCalc.Calculate(ref x);
                }
            }

            class Calculator { public void Compute(ref int x) { } }
            class NewCalculator { public void Calculate(ref int input) { } }
            """;

        var test = new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallCodeFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Код-фикс правильно обрабатывает out параметры")]
    public async Task ObsoleteCallCodeFixProvider_OutParameterExists_HandlesOutParameters()
    {
        // Arrange
        const string configJson = /*lang=json*/ """
            [{
                "source": {
                    "classFullName": "Calculator",
                    "methodName": "Compute"
                },
                "destination": {
                    "classFullName": "NewCalculator",
                    "methodName": "Calculate"
                },
                "mappings": [{
                    "sourceArgument": "x",
                    "destinationArgument": "input"
                }]
            }]
            """;

        const string sourceCode = /*lang=csharp*/ """
            class Program
            {
                private readonly NewCalculator _newCalc;

                public Program(NewCalculator nc) => _newCalc = nc;

                void Run()
                {
                    int x;
                    [|new Calculator().Compute(out x)|];
                }
            }

            class Calculator { public void Compute(out int x) { x = 0; } }
            class NewCalculator { public void Calculate(out int input) { input = 1; } }
            """;

        const string fixedCode = /*lang=csharp*/ """
            class Program
            {
                private readonly NewCalculator _newCalc;
            
                public Program(NewCalculator nc) => _newCalc = nc;
            
                void Run()
                {
                    int x;
                    _newCalc.Calculate(out x);
                }
            }

            class Calculator { public void Compute(out int x) { x = 0; } }
            class NewCalculator { public void Calculate(out int input) { input = 1; } }
            """;

        var test = new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallCodeFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Код-фикс корректно обрабатывает именованные аргументы")]
    public async Task ObsoleteCallCodeFixProvider_NamedArgumentsExists_HandlesNamedArguments()
    {
        // Arrange
        const string configJson = /*lang=json*/ """
            [{
                "source": {
                    "classFullName": "MathOld",
                    "methodName": "Calculate"
                },
                "destination": {
                    "classFullName": "MathNew",
                    "methodName": "Compute"
                },
                "mappings": [
                    {
                        "sourceArgument": "a",
                        "destinationArgument": "x"
                    },
                    {
                        "sourceArgument": "b",
                        "destinationArgument": "y"
                    }
                ]
            }]
            """;

        const string sourceCode = /*lang=csharp*/ """
            class Calculator
            {
                private readonly MathNew _math = new();
                void Calc() => [|new MathOld().Calculate(b: 2, a: 1)|];
            }

            class MathOld { public void Calculate(int a, int b) { } }
            class MathNew { public void Compute(int x, int y) { } }
            """;

        const string fixedCode = /*lang=csharp*/ """
            class Calculator
            {
                private readonly MathNew _math = new();
                void Calc() => _math.Compute(x: 1, y: 2);
            }

            class MathOld { public void Calculate(int a, int b) { } }
            class MathNew { public void Compute(int x, int y) { } }
            """;

        var test = new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallCodeFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Код-фикс корректно обрабатывает позиционные аргументы с учетом порядка")]
    public async Task ObsoleteCallCodeFixProvider_PositionalArgumentsExists_HandlesPositionalArguments()
    {
        const string configJson = /*lang=json*/ """
            [{
                "source": {
                    "classFullName": "PrinterV1",
                    "methodName": "Print"
                },
                "destination": {
                    "classFullName": "PrinterV2",
                    "methodName": "Render"
                },
                "mappings": [
                    { "sourceArgument": "text", "destinationArgument": "content" },
                    { "sourceArgument": "copies", "destinationArgument": "count" }
                ]
            }]
            """;

        const string sourceCode = /*lang=csharp*/ """
            class Document
            {
                private readonly PrinterV2 _printer = new();
            
                void Process() => [|new PrinterV1().Print("Hello", 3)|];
            }

            class PrinterV1 { public void Print(string text, int copies) { } }
            class PrinterV2 { public void Render(int count, string content) { } }
            """;

        const string fixedCode = /*lang=csharp*/ """
            class Document
            {
                private readonly PrinterV2 _printer = new();
            
                void Process() => _printer.Render(3, "Hello");
            }

            class PrinterV1 { public void Print(string text, int copies) { } }
            class PrinterV2 { public void Render(int count, string content) { } }
            """;

        var test = new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallCodeFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Код-фикс корректно обрабатывает сложную комбинацию позиционных " +
        "и именованных аргументов с разными типами")]
    public async Task ObsoleteCallCodeFixProvider_MultiTypeMixedArguments_HandlesComplexArgumentCombination()
    {
        // Arrange
        const string configJson = /*lang=json*/ """
            [{
                "source": {
                    "classFullName": "DataProcessor",
                    "methodName": "ProcessData"
                },
                "destination": {
                    "classFullName": "AdvancedDataProcessor",
                    "methodName": "ExecuteDataProcessing"
                },
                "mappings": [
                    { "sourceArgument": "input", "destinationArgument": "source" },
                    { "sourceArgument": "count", "destinationArgument": "quantity" },
                    { "sourceArgument": "enabled", "destinationArgument": "isActive" },
                    { "sourceArgument": "price", "destinationArgument": "cost" },
                    { "sourceArgument": "date", "destinationArgument": "timestamp" }
                ]
            }]
            """;

        const string sourceCode = /*lang=csharp*/ """
            class DataHandler
            {
                private readonly AdvancedDataProcessor _processor = new();

                void Handle()
                {
                    [|new DataProcessor().ProcessData("raw_data", 5, true, price: 99.99m, date: System.DateTime.Now)|];
                }
            }

            class DataProcessor
            {
                public void ProcessData(
                    string input,
                    int count,
                    bool enabled,
                    decimal price,
                    System.DateTime date)
                {
                    // ...
                }
            }

            class AdvancedDataProcessor
            {
                public void ExecuteDataProcessing(
                    string source,
                    bool isActive,
                    decimal cost,
                    int quantity,
                    System.DateTime timestamp)
                {
                    // ...
                }
            }
            """;

        const string fixedCode = /*lang=csharp*/ """
            class DataHandler
            {
                private readonly AdvancedDataProcessor _processor = new();

                void Handle()
                {
                    _processor.ExecuteDataProcessing("raw_data", true, cost: 99.99m, 5, timestamp: System.DateTime.Now);
                }
            }

            class DataProcessor
            {
                public void ProcessData(
                    string input,
                    int count,
                    bool enabled,
                    decimal price,
                    System.DateTime date)
                {
                    // ...
                }
            }

            class AdvancedDataProcessor
            {
                public void ExecuteDataProcessing(
                    string source,
                    bool isActive,
                    decimal cost,
                    int quantity,
                    System.DateTime timestamp)
                {
                    // ...
                }
            }
            """;

        var test = new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallCodeFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .Build();

        // Act & Assert
        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}