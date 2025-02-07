using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using ObsoleteMigrator.Analyzer.Configuration;
using ObsoleteMigrator.Analyzer.Configuration.Models;
using ObsoleteMigrator.Analyzer.Models;

namespace ObsoleteMigrator.Analyzer;

#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
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

    private static void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
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

        var configSourceText = configFile.GetText(compilationContext.CancellationToken);
        MigratorConfigurationProvider.Initialize(configSourceText);

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

        var migrationRecord = MigratorConfigurationProvider.Get(mappingKey);

        if (migrationRecord is null)
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

        var destinationTypeSymbol = semanticModel.Compilation
            .GetTypeByMetadataName(migrationRecord.Destination.ClassFullName);

        var destinationMethodSymbol = destinationTypeSymbol?
            .GetMembers(migrationRecord.Destination.MethodName)
            .OfType<IMethodSymbol>()
            .FirstOrDefault();

        if (destinationMethodSymbol == null)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            diagnosticProperties!,
            SymbolDisplay.ToDisplayString(symbolInfo.Symbol, SymbolDisplayFormat.MinimallyQualifiedFormat),
            SymbolDisplay.ToDisplayString(destinationMethodSymbol, SymbolDisplayFormat.MinimallyQualifiedFormat));

        nodeContext.ReportDiagnostic(diagnostic);
    }
}