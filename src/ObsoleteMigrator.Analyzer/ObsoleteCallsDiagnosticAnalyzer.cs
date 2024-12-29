using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using ObsoleteMigrator.Analyzer.Configuration;

namespace ObsoleteMigrator.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ObsoleteCallsDiagnosticAnalyzer : DiagnosticAnalyzer
{
    private MigratorConfiguration _migratorConfiguration = null!;

    internal const string DiagnosticId = "OCD0001";

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

            if (configFile == null)
            {
                return;
            }

            var configFileText = configFile.GetText()?.ToString();
            var configuration = MigratorConfiguration.CreateFromJson(configFileText);

            if (configuration == null)
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
            methodSymbol.Name,
            methodSymbol.ContainingType.ToDisplayString());

        if (migrationRecord == null)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
        nodeContext.ReportDiagnostic(diagnostic);
    }
}