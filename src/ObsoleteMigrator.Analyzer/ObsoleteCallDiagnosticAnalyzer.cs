using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using ObsoleteMigrator.Analyzer.Configuration;
using ObsoleteMigrator.Analyzer.Shared;

namespace ObsoleteMigrator.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ObsoleteCallDiagnosticAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        MigratorConstants.DiagnosticId,
        MigratorConstants.DiagnosticTitle,
        MigratorConstants.MessageFormat,
        MigratorConstants.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
    {
        var additionalFiles = compilationContext.Options.AdditionalFiles;

        var configFile = additionalFiles
            .SingleOrDefault(file => file.Path.EndsWith(
                MigratorConstants.ConfigurationFilePath,
                StringComparison.OrdinalIgnoreCase));

        if (configFile is null)
        {
            return;
        }

        var configFileText = configFile.GetText()?.ToString();
        var isValidConfigurationProvided = MigratorConfiguration.TryInitialize(configFileText);

        if (!isValidConfigurationProvided)
        {
            return;
        }

        compilationContext.RegisterSyntaxNodeAction(
            AnalyzeInvocationExpression,
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext nodeContext)
    {
        var semanticModel = nodeContext.SemanticModel;
        var invocation = (InvocationExpressionSyntax)nodeContext.Node;

        var symbolInfo = ModelExtensions.GetSymbolInfo(semanticModel, invocation, nodeContext.CancellationToken);

        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        var mappingKey = new MappingKey(
            methodSymbol.ContainingType.ToDisplayString(),
            methodSymbol.Name);

        if (!MigratorConfiguration.ContainsMigrationRecord(mappingKey))
        {
            return;
        }

        var diagnosticProperties =
            new Dictionary<string, string>
                {
                    { nameof(MappingKey.DisplayType), mappingKey.DisplayType },
                    { nameof(MappingKey.MethodName), mappingKey.MethodName }
                }
                .ToImmutableDictionary();

        var diagnostic = Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            diagnosticProperties!);

        nodeContext.ReportDiagnostic(diagnostic);
    }
}