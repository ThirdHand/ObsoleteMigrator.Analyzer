using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ObsoleteMigrator.Analyzer.Configuration;
using ObsoleteMigrator.Analyzer.Configuration.Models;
using ObsoleteMigrator.Analyzer.Shared;

namespace ObsoleteMigrator.Analyzer;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ObsoleteCallFixProvider))]
public class ObsoleteCallFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(MigratorConstants.DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.SingleOrDefault();
        if (diagnostic is null)
        {
            return;
        }

        var root = await diagnostic.Location.SourceTree!.GetRootAsync(context.CancellationToken);
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var displayType = diagnostic.Properties[nameof(MappingKey.DisplayType)]!;
        var methodName = diagnostic.Properties[nameof(MappingKey.MethodName)]!;

        var mappingKey = new MappingKey(displayType, methodName);
        var migrationRecord = MigratorConfiguration.GetMigrationRecord(mappingKey);

        context.RegisterCodeFix(
            CodeAction.Create(
                MigratorConstants.CodeFixTitle,
                token => PerformCodeFix(context.Document, migrationRecord, root, invocation, token),
                MigratorConstants.DiagnosticId),
            diagnostic);
    }

    private static async Task<Document> PerformCodeFix(
        Document document,
        MigrationRecord migrationRecord,
        SyntaxNode root,
        InvocationExpressionSyntax oldInvocation,
        CancellationToken cancellationToken)
    {
        var classDeclaration = oldInvocation.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDeclaration is null)
        {
            return document;
        }

        var semanticModel = (await document.GetSemanticModelAsync(cancellationToken))!;

        var destinationFieldDeclaration = GetExistingDestinationField(
            classDeclaration,
            semanticModel,
            migrationRecord.Destination.ClassFullName);

        string? fieldName = null;
        if (destinationFieldDeclaration is null)
        {
            EnsureUsingDirectiveExists(
                semanticModel,
                migrationRecord.Destination.ClassFullName,
                root,
                ref document);

            destinationFieldDeclaration = CreateDestinationField(
                migrationRecord.Destination.ClassFullName,
                ref classDeclaration);

            var constructorDeclaration = EnsureConstructorDeclarationExists(ref classDeclaration);

            fieldName = GetFieldNameFromFieldDeclaration(destinationFieldDeclaration);
            var fieldTypeName = destinationFieldDeclaration.Declaration.Type.ToString();

            CreateConstructorParameter(fieldName, fieldTypeName, ref constructorDeclaration);
            CreateConstructorAssignment(fieldName, fieldTypeName, constructorDeclaration, ref classDeclaration);
        }

        fieldName ??= GetFieldNameFromFieldDeclaration(destinationFieldDeclaration);

        return ReplaceObsoleteCall(
            semanticModel,
            fieldName,
            migrationRecord,
            oldInvocation,
            root,
            document);
    }

    private static string GetFieldNameFromFieldDeclaration(FieldDeclarationSyntax fieldDeclaration)
    {
        return fieldDeclaration.Declaration.Variables.Single().Identifier.Text;
    }

    private static void CreateConstructorAssignment(
        string fieldName,
        string fieldTypeName,
        ConstructorDeclarationSyntax constructorDeclaration,
        ref ClassDeclarationSyntax classDeclaration)
    {
        var assignment = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(fieldName),
                SyntaxFactory.IdentifierName(fieldTypeName)));

        constructorDeclaration = constructorDeclaration
            .WithBody(constructorDeclaration.Body!.AddStatements(assignment));

        classDeclaration = classDeclaration.ReplaceNode(
            classDeclaration.Members.OfType<ConstructorDeclarationSyntax>().First(),
            constructorDeclaration);
    }

    private static void CreateConstructorParameter(
        string fieldName,
        string fieldTypeName,
        ref ConstructorDeclarationSyntax constructorDeclaration)
    {
        var constructorParam =
            SyntaxFactory
                .Parameter(SyntaxFactory.Identifier(fieldName.TrimStart('_')))
                .WithType(SyntaxFactory.IdentifierName(fieldTypeName));

        constructorDeclaration = constructorDeclaration.AddParameterListParameters(constructorParam);
    }

    private static ConstructorDeclarationSyntax EnsureConstructorDeclarationExists(
        ref ClassDeclarationSyntax classDeclaration)
    {
        var constructor = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault();

        if (constructor is null)
        {
            constructor =
                SyntaxFactory
                    .ConstructorDeclaration(classDeclaration.Identifier)
                    .WithModifiers(
                        SyntaxFactory.TokenList(
                            SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithBody(SyntaxFactory.Block());

            classDeclaration = classDeclaration.AddMembers(constructor);
        }

        return constructor;
    }

    private static FieldDeclarationSyntax? GetExistingDestinationField(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        string destinationClassFullName)
    {
        return classDeclaration.Members
            .OfType<FieldDeclarationSyntax>()
            .SingleOrDefault(f =>
            {
                var variableType = f.Declaration.Type;
                var symbolInfo = ModelExtensions.GetSymbolInfo(semanticModel, variableType).Symbol;

                return f.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) &&
                    symbolInfo?.ToDisplayString() == destinationClassFullName;
            });
    }

    private static void EnsureUsingDirectiveExists(
        SemanticModel semanticModel,
        string classFullName,
        SyntaxNode root,
        ref Document document)
    {
        var usings = root
            .DescendantNodesAndSelf()
            .OfType<UsingDirectiveSyntax>()
            .ToList();

        var destinationType = semanticModel.Compilation.GetTypeByMetadataName(classFullName);
        var targetNamespace = destinationType?.ContainingNamespace?.ToDisplayString();

        if (targetNamespace == "<global namespace>" || usings.Any(u => u.Name.ToString() == targetNamespace))
        {
            return;
        }

        var newUsingDirective = SyntaxFactory.UsingDirective(
            SyntaxFactory.ParseName(targetNamespace!));

        var namespaceDeclaration = root
            .DescendantNodesAndSelf()
            .OfType<NamespaceDeclarationSyntax>()
            .First();

        var newRoot = root.InsertNodesBefore(namespaceDeclaration, [newUsingDirective]);

        document = document.WithSyntaxRoot(newRoot);
    }

    private static FieldDeclarationSyntax CreateDestinationField(
        string classFullName,
        ref ClassDeclarationSyntax classDeclaration)
    {
        var classShortName = classFullName.Split('.').Last();
        var fieldName = $"_{char.ToLower(classShortName[0])}{classShortName.Substring(1)}";

        var fieldDeclaration =
            SyntaxFactory
                .FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.IdentifierName(classShortName))
                        .WithVariables(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.VariableDeclarator(
                                    SyntaxFactory.Identifier(fieldName)))))
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));

        classDeclaration = classDeclaration.AddMembers(fieldDeclaration);

        return fieldDeclaration;
    }

    private static Document ReplaceObsoleteCall(
        SemanticModel semanticModel,
        string fieldName,
        MigrationRecord migrationRecord,
        InvocationExpressionSyntax oldInvocation,
        SyntaxNode root,
        Document document)
    {
        var methodSymbol = semanticModel.GetSymbolInfo(oldInvocation).Symbol as IMethodSymbol;
        var parameters = methodSymbol?.Parameters;

        var paramsMapping = migrationRecord.Mappings
            .ToDictionary(x => x.SourceArgument);

        var newArguments = oldInvocation.ArgumentList.Arguments
            .Select((x, index) =>
            {
                var parameter = parameters?[index];
                var oldParamName = x.NameColon?.Name.Identifier.Text ?? parameter!.Name;

                if (!paramsMapping.TryGetValue(oldParamName, out var mapping))
                {
                    return null!;
                }

                return SyntaxFactory.Argument(
                    SyntaxFactory.IdentifierName(
                        mapping.DestinationArgument));
            })
            .Where(x => x is not null)
            .ToArray();

        var newInvocation =
            SyntaxFactory
                .InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(fieldName),
                        SyntaxFactory.IdentifierName(migrationRecord.Destination.MethodName)))
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(newArguments)));

        root = root.ReplaceNode(oldInvocation, newInvocation);

        return document.WithSyntaxRoot(root);
    }
}