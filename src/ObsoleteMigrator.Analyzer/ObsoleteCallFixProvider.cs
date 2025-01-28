using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using ObsoleteMigrator.Analyzer.Configuration;
using ObsoleteMigrator.Analyzer.Configuration.Models;
using ObsoleteMigrator.Analyzer.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ObsoleteMigrator.Analyzer;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ObsoleteCallFixProvider))]
public class ObsoleteCallFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [MigratorConstants.DiagnosticId];

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
        var oldClassDeclaration = oldInvocation.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (oldClassDeclaration is null)
        {
            return document;
        }

        var semanticModel = (await document.GetSemanticModelAsync(cancellationToken))!;

        var destinationFieldDeclaration = GetExistingDestinationField(
            oldClassDeclaration,
            semanticModel,
            migrationRecord.Destination.ClassFullName);

        string? fieldName = null;
        root = root.TrackNodes(oldInvocation, oldClassDeclaration);

        if (destinationFieldDeclaration is null)
        {
            EnsureUsingDirectiveExists(
                semanticModel,
                migrationRecord.Destination.ClassFullName,
                ref root);

            var trackedOldClassDeclaration = root.GetCurrentNode(oldClassDeclaration)!;
            var newClassDeclaration = trackedOldClassDeclaration;
            var fieldInsertionIndex = GetLastAppropriateFieldPosition(oldClassDeclaration.Members);

            destinationFieldDeclaration = CreateDestinationField(
                semanticModel,
                migrationRecord.Destination.ClassFullName,
                fieldInsertionIndex,
                ref newClassDeclaration);

            var ctorInsertionIndex = fieldInsertionIndex + 1;
            var constructorDeclaration = EnsureConstructorDeclarationExists(ctorInsertionIndex, ref newClassDeclaration);

            fieldName = GetFieldNameFromFieldDeclaration(destinationFieldDeclaration);
            var fieldTypeName = destinationFieldDeclaration.Declaration.Type.ToString();

            CreateConstructorParameter(fieldName, fieldTypeName, ref constructorDeclaration);
            CreateConstructorAssignment(fieldName, constructorDeclaration, ref newClassDeclaration);

            root = root.ReplaceNode(trackedOldClassDeclaration, newClassDeclaration);
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
        ConstructorDeclarationSyntax constructorDeclaration,
        ref ClassDeclarationSyntax classDeclaration)
    {
        var assignment = ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(fieldName),
                IdentifierName(fieldName.TrimStart('_'))));

        constructorDeclaration = constructorDeclaration
            .WithBody(constructorDeclaration.Body!
                .AddStatements(assignment))
            .NormalizeWhitespace()
            .WithTrailingTrivia(ElasticCarriageReturnLineFeed);

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
            Parameter(Identifier(fieldName.TrimStart('_')))
                .WithType(IdentifierName(fieldTypeName));

        constructorDeclaration = constructorDeclaration.AddParameterListParameters(constructorParam);
    }

    private static ConstructorDeclarationSyntax EnsureConstructorDeclarationExists(
        int insertionIndex,
        ref ClassDeclarationSyntax classDeclaration)
    {
        var constructor = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault();

        if (constructor is null)
        {
            constructor = ConstructorDeclaration(classDeclaration.Identifier)
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithBody(Block());

            var newClassMembers = classDeclaration.Members.Insert(insertionIndex, constructor);

            classDeclaration = classDeclaration.WithMembers(newClassMembers);
        }

        return constructor;
    }

    private static int GetLastAppropriateFieldPosition(IEnumerable<MemberDeclarationSyntax> members)
    {
        var membersArray = members.ToArray();

        var lastFieldIndex = -1;
        var lastReadonlyFieldIndex = -1;

        for (var i = 0; i < membersArray.Length; i++)
        {
            var member = membersArray[i];

            if (member is not FieldDeclarationSyntax fds)
            {
                continue;
            }

            lastFieldIndex = i;

            if (fds.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)))
            {
                lastReadonlyFieldIndex = i;
            }
        }

        return lastReadonlyFieldIndex == -1
            ? lastFieldIndex + 1
            : lastReadonlyFieldIndex + 1;
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
        ref SyntaxNode root)
    {
        var destinationType = semanticModel.Compilation.GetTypeByMetadataName(classFullName);
        var targetNamespace = destinationType!.ContainingNamespace!.ToDisplayString();

        var compilationUnit = (root as CompilationUnitSyntax)!;
        var targetUsingAlreadyAdded = compilationUnit.Usings
            .Any(u => u.Name!.ToString() == targetNamespace);

        if (targetUsingAlreadyAdded)
        {
            return;
        }

        var newUsingDirective = UsingDirective(ParseName(targetNamespace))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
            .NormalizeWhitespace()
            .WithTrailingTrivia(ElasticCarriageReturnLineFeed);

        root = compilationUnit.AddUsings(newUsingDirective);
    }

    private static FieldDeclarationSyntax CreateDestinationField(
        SemanticModel semanticModel,
        string classFullName,
        int insertionIndex,
        ref ClassDeclarationSyntax classDeclaration)
    {
        var destinationType = semanticModel.Compilation.GetTypeByMetadataName(classFullName);
        string classShortName;

        if (destinationType != null)
        {
            var isStandardInterface =
                destinationType is { TypeKind: TypeKind.Interface, Name.Length: > 1 } &&
                destinationType.Name[0] == 'I' &&
                char.IsUpper(destinationType.Name[1]);

            classShortName = isStandardInterface
                ? destinationType.Name.Substring(1)
                : destinationType.Name;
        }
        else
        {
            classShortName = classFullName.Split('.').Last();
        }

        var fieldName = $"_{char.ToLower(classShortName[0])}{classShortName.Substring(1)}";

        var variableDeclarator = VariableDeclarator(Identifier(fieldName));
        var variableDeclaration = VariableDeclaration(IdentifierName(classShortName))
            .WithVariables(SingletonSeparatedList(variableDeclarator));

        var modifiers = TokenList(
            Token(SyntaxKind.PrivateKeyword),
            Token(SyntaxKind.ReadOnlyKeyword));

        var fieldDeclaration = FieldDeclaration(variableDeclaration)
            .WithModifiers(modifiers)
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
            .NormalizeWhitespace()
            .WithTrailingTrivia(ElasticCarriageReturnLineFeed);

        var newClassMembers = classDeclaration.Members.Insert(insertionIndex, fieldDeclaration);

        classDeclaration = classDeclaration.WithMembers(newClassMembers);

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
        var destinationType = semanticModel.Compilation.GetTypeByMetadataName(
            migrationRecord.Destination.ClassFullName);

        var destinationMethod = destinationType?
            .GetMembers(migrationRecord.Destination.MethodName)
            .OfType<IMethodSymbol>()
            .FirstOrDefault();

        if (destinationMethod == null)
        {
            return document;
        }

        var sourceArguments = MapArgumentsByName(oldInvocation, semanticModel);
        var newArguments = new List<ArgumentSyntax>();

        foreach (var destParameter in destinationMethod.Parameters)
        {
            var sourceArg = migrationRecord.Mappings
                .Where(m => m.DestinationArgument == destParameter.Name)
                .Select(m => sourceArguments
                    .TryGetValue(m.SourceArgument, out var sourceArg)
                    ? sourceArg
                    : null)
                .SingleOrDefault();

            if (sourceArg != null)
            {
                newArguments.Add(CreateArgument(sourceArg, destParameter));
            }
            else if (!destParameter.HasExplicitDefaultValue)
            {
                newArguments.Add(Argument(LiteralExpression(SyntaxKind.DefaultLiteralExpression)));
            }
        }

        var newInvocation = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(fieldName),
                    IdentifierName(migrationRecord.Destination.MethodName)))
            .WithArgumentList(ArgumentList(SeparatedList(newArguments)));

        oldInvocation = root.GetCurrentNode(oldInvocation)!;

        root = root
            .ReplaceNode(oldInvocation, newInvocation)
            .WithAdditionalAnnotations(Formatter.Annotation);

        return document.WithSyntaxRoot(root);
    }

    private static Dictionary<string, ArgumentSyntax> MapArgumentsByName(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        var parameters = methodSymbol?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty;

        var argumentsLookup = new Dictionary<string, ArgumentSyntax>(StringComparer.OrdinalIgnoreCase);
        var arguments = invocation.ArgumentList.Arguments;

        for (var i = 0; i < arguments.Count; i++)
        {
            var paramName = arguments[i].NameColon?.Name.Identifier.Text ?? parameters.ElementAtOrDefault(i)?.Name;

            if (paramName != null && !argumentsLookup.ContainsKey(paramName))
            {
                argumentsLookup.Add(paramName, arguments[i]);
            }
        }

        return argumentsLookup;
    }

    private static ArgumentSyntax CreateArgument(ArgumentSyntax sourceArg, IParameterSymbol destParameter)
    {
        var newName = sourceArg.NameColon is not null
            ? NameColon(destParameter.Name)
            : null;

        SyntaxToken? refKindToken = destParameter.RefKind switch
        {
            RefKind.Ref => Token(SyntaxKind.RefKeyword),
            RefKind.Out => Token(SyntaxKind.OutKeyword),
            RefKind.In => Token(SyntaxKind.InKeyword),
            _ => null
        };

        var argument = sourceArg
            .WithNameColon(newName)
            .WithExpression(sourceArg.Expression);

        return refKindToken is not null
            ? argument.WithRefOrOutKeyword(refKindToken.Value)
            : argument;
    }
}