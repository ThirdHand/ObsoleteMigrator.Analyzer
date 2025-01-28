using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using ObsoleteMigrator.Analyzer.Shared;
using Xunit;

namespace ObsoleteMigrator.Analyzer.Tests;

public class ObsoleteCallFixProviderTests
{
    [Fact(DisplayName = "Код фикс создает поле, конструктор и using при отсутствии поля")]
    public async Task FixCreatesFieldAndConstructor_WhenDestinationFieldNotExists()
    {
        const string configJson = /*lang=json*/ """
            [{
                "source": {"classFullName": "Bar", "methodName": "OldMethod" },
                "destination": { "classFullName": "External.NewBar", "methodName": "NewMethod" },
                "mappings": []
            }]
            """;

        var sourceCode = /*lang=csharp*/ """
            using System;

            class Foo
            {
                private readonly int _test;
                
                public void Test()
                {
                    new Bar().OldMethod();
                }
            }

            class Bar { public void OldMethod() { } }
            namespace External { public class NewBar { public void NewMethod() { } } }
            """;

        var fixedCode = /*lang=csharp*/ """
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

        var expectedDiagnostic = new DiagnosticResult(MigratorConstants.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(9, 9);

        var test = new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithExpectedDiagnostics(expectedDiagnostic)
            .WithFixedCode(fixedCode)
            .Build();

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Код фикс использует существующее поле назначения")]
    public async Task FixUsesExistingField_WhenDestinationFieldExists()
    {
        const string configJson = /*lang=json*/ """
                [{
                    "source": { "classFullName": "Bar", "methodName": "OldMethod" },
                    "destination": { "classFullName": "NewBar", "methodName": "NewMethod" },
                    "mappings": []
                }]
            """;

        var sourceCode = /*lang=csharp*/ """
                using System;
            
                class Foo
                {
                    private readonly NewBar _newBar;
            
                    public Foo(NewBar nb) => _newBar = nb;
            
                    public void Test() => new Bar().OldMethod();
                }
            
                class Bar { public void OldMethod() { } }
                class NewBar { public void NewMethod() { } }
            """;

        var fixedCode = /*lang=csharp*/ """
                using System;
            
                class Foo
                {
                    private readonly NewBar _newBar;
            
                    public Foo(NewBar nb) => _newBar = nb;
            
                    public void Test() => _newBar.NewMethod();
                }
            
                class Bar { public void OldMethod() { } }
                class NewBar { public void NewMethod() { } }
            """;

        var test = new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .Build();

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "Код фикс добавляет ref параметр")]
    public async Task FixHandlesRefParameters_Correctly()
    {
        const string configJson = /*lang=json*/ """
                [{
                    "source": { "classFullName": "Calculator", "methodName": "Compute" },
                    "destination": { "classFullName": "NewCalculator", "methodName": "Calculate" },
                    "mappings": [{ "sourceArgument": "x", "destinationArgument": "input" }]
                }]
            """;

        var sourceCode = /*lang=csharp*/ """
                class Program
                {
                    private readonly NewCalculator _newCalc;
            
                    public Program(NewCalculator nc) => _newCalc = nc;
            
                    void Run()
                    {
                        int x = 5;
                        new Calculator().Compute(ref x);
                    }
                }
            
                class Calculator { public void Compute(ref int x) { } }
                class NewCalculator { public void Calculate(ref int input) { } }
            """;

        var fixedCode = /*lang=csharp*/ """
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

        var test = new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .Build();

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    // Тест 1: Создание поля и конструктора (обратный кейс для пункта 2)
    [Fact(DisplayName = "Создает поле и конструктор при отсутствии существующего")]
    public async Task FixCreatesFieldAndConstructor_WhenNoExistingField()
    {
        const string configJson = /*lang=json*/ """
                [{
                    "source": { "classFullName": "LegacyService", "methodName": "DeprecatedCall" },
                    "destination": { "classFullName": "NewService", "methodName": "ModernCall" },
                    "mappings": []
                }]
            """;

        var sourceCode = /*lang=csharp*/ """
                class Program
                {
                    void Main() => new LegacyService().DeprecatedCall();
                }
                
                class LegacyService { public void DeprecatedCall() {} }
                namespace Services { public class NewService { public void ModernCall() {} } }
            """;

        var fixedCode = /*lang=csharp*/ """
                using Services;
            
                class Program
                {
                    private readonly NewService _newService;
                
                    public Program(NewService newService)
                    {
                        _newService = newService;
                    }
                
                    void Main() => _newService.ModernCall();
                }
                
                class LegacyService { public void DeprecatedCall() {} }
                namespace Services { public class NewService { public void ModernCall() {} } }
            """;

        await new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .WithExpectedDiagnostics(ExpectedDiagnostic.AtLine(3, 25))
            .Build()
            .RunAsync(TestContext.Current.CancellationToken);
    }

    // Тест 2: Использование существующего поля (оригинальный кейс)
    [Fact(DisplayName = "Использует существующее поле назначения")]
    public async Task UsesExistingField_WhenFieldAlreadyExists()
    {
        const string configJson = /*lang=json*/ """
                [{
                    "source": { "classFullName": "OldEngine", "methodName": "Start" },
                    "destination": { "classFullName": "NewEngine", "methodName": "PowerOn" },
                    "mappings": []
                }]
            """;

        var sourceCode = /*lang=csharp*/ """
                class Car
                {
                    private readonly NewEngine _engine = new();
                    void StartCar() => new OldEngine().Start();
                }
                
                class OldEngine { public void Start() {} }
                class NewEngine { public void PowerOn() {} }
            """;

        var fixedCode = /*lang=csharp*/ """
                class Car
                {
                    private readonly NewEngine _engine = new();
                    void StartCar() => _engine.PowerOn();
                }
                
                class OldEngine { public void Start() {} }
                class NewEngine { public void PowerOn() {} }
            """;

        await new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .WithExpectedDiagnostics(ExpectedDiagnostic.AtLine(4, 25))
            .Build()
            .RunAsync(TestContext.Current.CancellationToken);
    }

    // Тест 3: Проверка существующего using
    [Fact(DisplayName = "Не добавляет существующий using")]
    public async Task DoesNotAddDuplicateUsing_WhenNamespaceExists()
    {
        const string configJson = /*lang=json*/ """
                [{
                    "source": { "classFullName": "Legacy", "methodName": "Run" },
                    "destination": { "classFullName": "Modern.Implementation", "methodName": "Execute" },
                    "mappings": []
                }]
            """;

        var sourceCode = /*lang=csharp*/ """
                using Modern;
                
                class App
                {
                    void Launch() => new Legacy().Run();
                }
                
                class Legacy { public void Run() {} }
                namespace Modern { public class Implementation { public void Execute() {} } }
            """;

        var fixedCode = /*lang=csharp*/ """
                using Modern;
                
                class App
                {
                    private readonly Implementation _implementation;
                
                    public App(Implementation implementation)
                    {
                        _implementation = implementation;
                    }
                
                    void Launch() => _implementation.Execute();
                }
                
                class Legacy { public void Run() {} }
                namespace Modern { public class Implementation { public void Execute() {} } }
            """;

        await new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .WithExpectedDiagnostics(ExpectedDiagnostic.AtLine(5, 25))
            .Build()
            .RunAsync(TestContext.Current.CancellationToken);
    }

    // Тест 4: Добавление недостающего using
    [Fact(DisplayName = "Добавляет недостающий using")]
    public async Task AddsMissingUsing_WhenNamespaceNotImported()
    {
        const string configJson = /*lang=json*/ """
                [{
                    "source": { "classFullName": "ObsoleteTool", "methodName": "Use" },
                    "destination": { "classFullName": "NewTools.AdvancedTool", "methodName": "Operate" },
                    "mappings": []
                }]
            """;

        var sourceCode = /*lang=csharp*/ """
            class Workshop
            {
                void Work() => new ObsoleteTool().Use();
            }
            
            class ObsoleteTool { public void Use() {} }
            namespace NewTools { public class AdvancedTool { public void Operate() {} } }
            """;

        var fixedCode = /*lang=csharp*/ """
            using NewTools;
            
            class Workshop
            {
                private readonly AdvancedTool _advancedTool;
            
                public Workshop(AdvancedTool advancedTool)
                {
                    _advancedTool = advancedTool;
                }
            
                void Work() => _advancedTool.Operate();
            }
            
            class ObsoleteTool { public void Use() {} }
            namespace NewTools { public class AdvancedTool { public void Operate() {} } }
            """;

        await new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .WithExpectedDiagnostics(ExpectedDiagnostic.AtLine(3, 20))
            .Build()
            .RunAsync(TestContext.Current.CancellationToken);
    }

    // Тест 5: Обработка именованных аргументов
    [Fact(DisplayName = "Корректно обрабатывает именованные аргументы")]
    public async Task HandlesNamedArguments_Correctly()
    {
        const string configJson = /*lang=json*/ """
                [{
                    "source": { "classFullName": "MathOld", "methodName": "Calculate" },
                    "destination": { "classFullName": "MathNew", "methodName": "Compute" },
                    "mappings": [
                        { "sourceArgument": "a", "destinationArgument": "x" },
                        { "sourceArgument": "b", "destinationArgument": "y" }
                    ]
                }]
            """;

        var sourceCode = /*lang=csharp*/ """
                class Calculator
                {
                    private readonly MathNew _math = new();
                    void Calc() => new MathOld().Calculate(b: 2, a: 1);
                }
                
                class MathOld { public void Calculate(int a, int b) {} }
                class MathNew { public void Compute(int x, int y) {} }
            """;

        var fixedCode = /*lang=csharp*/ """
                class Calculator
                {
                    private readonly MathNew _math = new();
                    void Calc() => _math.Compute(x: 1, y: 2);
                }
                
                class MathOld { public void Calculate(int a, int b) {} }
                class MathNew { public void Compute(int x, int y) {} }
            """;

        await new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .WithExpectedDiagnostics(ExpectedDiagnostic.AtLine(4, 25))
            .Build()
            .RunAsync(TestContext.Current.CancellationToken);
    }

    // Тест 6: Обработка позиционных аргументов
    [Fact(DisplayName = "Корректно обрабатывает позиционные аргументы")]
    public async Task HandlesPositionalArguments_Correctly()
    {
        const string configJson = /*lang=json*/ """
                [{
                    "source": { "classFullName": "PrinterV1", "methodName": "Print" },
                    "destination": { "classFullName": "PrinterV2", "methodName": "Render" },
                    "mappings": [
                        { "sourceArgument": "text", "destinationArgument": "content" },
                        { "sourceArgument": "copies", "destinationArgument": "count" }
                    ]
                }]
            """;

        var sourceCode = /*lang=csharp*/ """
                class Document
                {
                    private readonly PrinterV2 _printer = new();
                    void Process() => new PrinterV1().Print("Hello", 3);
                }
                
                class PrinterV1 { public void Print(string text, int copies) {} }
                class PrinterV2 { public void Render(string content, int count) {} }
            """;

        var fixedCode = /*lang=csharp*/ """
                class Document
                {
                    private readonly PrinterV2 _printer = new();
                    void Process() => _printer.Render(content: "Hello", count: 3);
                }
                
                class PrinterV1 { public void Print(string text, int copies) {} }
                class PrinterV2 { public void Render(string content, int count) {} }
            """;

        await new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .WithExpectedDiagnostics(ExpectedDiagnostic.AtLine(4, 25))
            .Build()
            .RunAsync(TestContext.Current.CancellationToken);
    }

    // Тест 7: Обработка смешанных аргументов
    [Fact(DisplayName = "Корректно обрабатывает смешанные аргументы")]
    public async Task HandlesMixedArguments_Correctly()
    {
        const string configJson = /*lang=json*/ """
                [{
                    "source": { "classFullName": "Converter", "methodName": "Convert" },
                    "destination": { "classFullName": "AdvancedConverter", "methodName": "Transform" },
                    "mappings": [
                        { "sourceArgument": "input", "destinationArgument": "source" },
                        { "sourceArgument": "format", "destinationArgument": "options" }
                    ]
                }]
            """;

        var sourceCode = /*lang=csharp*/ """
                class DataProcessor
                {
                    private readonly AdvancedConverter _converter = new();
                    void Process() => new Converter().Convert("data", format: "json");
                }
                
                class Converter { public void Convert(string input, string format) {} }
                class AdvancedConverter { public void Transform(string source, string options) {} }
            """;

        var fixedCode = /*lang=csharp*/ """
                class DataProcessor
                {
                    private readonly AdvancedConverter _converter = new();
                    void Process() => _converter.Transform(source: "data", options: "json");
                }
                
                class Converter { public void Convert(string input, string format) {} }
                class AdvancedConverter { public void Transform(string source, string options) {} }
            """;

        await new CodeFixTestBuilder<ObsoleteCallDiagnosticAnalyzer, ObsoleteCallFixProvider>()
            .WithSource(sourceCode)
            .WithConfiguration(configJson)
            .WithFixedCode(fixedCode)
            .WithExpectedDiagnostics(ExpectedDiagnostic.AtLine(4, 25))
            .Build()
            .RunAsync(TestContext.Current.CancellationToken);
    }
}

public static class ExpectedDiagnostic
{
    public static DiagnosticResult AtLine(int line, int column) =>
        new DiagnosticResult(MigratorConstants.DiagnosticId, DiagnosticSeverity.Warning)
            .WithLocation(line, column);
}