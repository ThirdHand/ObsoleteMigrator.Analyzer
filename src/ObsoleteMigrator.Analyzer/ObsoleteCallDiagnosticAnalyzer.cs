using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using ObsoleteMigrator.Analyzer.Configuration;

namespace ObsoleteMigrator.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ObsoleteCallDiagnosticAnalyzer : DiagnosticAnalyzer
{
    private MigratorConfiguration _migratorConfiguration = null!;

    public const string DiagnosticId = "OCD0001";

    private const string ConfigurationFilePath = "ObsoleteMigrator.json";
    private const string Title = "Title";
    private const string Category = "Obsolete";
    private const string MessageFormat = "Obsolete migrator analyzer";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var additionalFiles = compilationContext.Options.AdditionalFiles;

            var configFile = additionalFiles
                .SingleOrDefault(file => file.Path.EndsWith(ConfigurationFilePath, StringComparison.OrdinalIgnoreCase));

            if (configFile is null)
            {
                return;
            }

            var configFileText = configFile.GetText()?.ToString();
            var configuration = MigratorConfiguration.CreateFromJson(configFileText);

            if (configuration is null)
            {
                return;
            }

            _migratorConfiguration = configuration;

            compilationContext.RegisterSyntaxNodeAction(
                AnalyzeInvocationExpression,
                SyntaxKind.InvocationExpression);
        });
    }

    private void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext nodeContext)
    {
        var semanticModel = nodeContext.SemanticModel;
        var invocation = (InvocationExpressionSyntax)nodeContext.Node;

        var symbolInfo = ModelExtensions.GetSymbolInfo(semanticModel, invocation, nodeContext.CancellationToken);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        var migrationRecord = _migratorConfiguration.GetMigrationRecord(
            methodSymbol.ContainingType.ToDisplayString(),
            methodSymbol.Name);

        if (migrationRecord is null)
        {
            return;
        }

        var migrationRecordJson = JsonSerializer.Serialize(migrationRecord);

        var diagnosticProperties = ImmutableDictionary<string, string>.Empty
            .Add(nameof(migrationRecord), migrationRecordJson);

        var diagnostic = Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            diagnosticProperties!);

        nodeContext.ReportDiagnostic(diagnostic);
    }
}