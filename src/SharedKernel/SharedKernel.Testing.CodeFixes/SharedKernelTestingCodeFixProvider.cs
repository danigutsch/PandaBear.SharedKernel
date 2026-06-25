using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;
using SharedKernel.Testing.Analyzers;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SharedKernel.Testing.CodeFixes;

/// <summary>
/// Placeholder code-fix provider for implemented SharedKernel testing diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SharedKernelTestingCodeFixProvider))]
public sealed class SharedKernelTestingCodeFixProvider : CodeFixProvider
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TestingDiagnosticIds.TestMethodWarningSuppression, TestingDiagnosticIds.XunitTestMethodNaming, TestingDiagnosticIds.XunitTestMethodRequiredTrait, TestingDiagnosticIds.XunitTestClassHelperMethod];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
    {
        return SafeTestingFixAllProvider.Instance;
    }

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null)
        {
            return;
        }

        var document = context.Document;
        var syntaxRoot = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (syntaxRoot is null)
        {
            return;
        }

        if (string.Equals(diagnostic.Id, TestingDiagnosticIds.TestMethodWarningSuppression, StringComparison.Ordinal))
        {
            RegisterRemovePragmaFix(context, document, diagnostic, syntaxRoot);
            return;
        }

        if (syntaxRoot.FindNode(context.Span).FirstAncestorOrSelf<MethodDeclarationSyntax>() is not MethodDeclarationSyntax methodDeclaration)
        {
            return;
        }

        if (string.Equals(diagnostic.Id, TestingDiagnosticIds.XunitTestMethodRequiredTrait, StringComparison.Ordinal))
        {
            RegisterRequiredTraitFix(context, document, diagnostic, methodDeclaration, syntaxRoot);
            return;
        }

        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel?.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (string.Equals(diagnostic.Id, TestingDiagnosticIds.XunitTestClassHelperMethod, StringComparison.Ordinal))
        {
            RegisterMoveHelperMethodFix(context, document, methodDeclaration, methodSymbol, syntaxRoot, semanticModel, context.CancellationToken);
            return;
        }

        var targetName = TryConvertToUnderscoreName(methodSymbol.Name);
        if (targetName is null
            || string.Equals(targetName, methodSymbol.Name, StringComparison.Ordinal)
            || HasRenameConflict(semanticModel, methodDeclaration, methodSymbol, targetName))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Rename to '{targetName}'",
                createChangedSolution: ct => RenameSymbolAsync(document.Project.Solution, methodSymbol, targetName, ct),
                equivalenceKey: $"RenameXunitTestMethod:{targetName}"),
            diagnostic);
    }

    private static void RegisterRemovePragmaFix(
        CodeFixContext context,
        Document document,
        Diagnostic diagnostic,
        SyntaxNode syntaxRoot)
    {
        var trivia = syntaxRoot
            .DescendantTrivia(descendIntoTrivia: true)
            .FirstOrDefault(candidate => candidate.Span.IntersectsWith(context.Span) && candidate.GetStructure() is PragmaWarningDirectiveTriviaSyntax);
        if (trivia.RawKind == 0)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Remove pragma warning directive",
                createChangedDocument: _ => RemovePragmaDirective(document, syntaxRoot, trivia),
                equivalenceKey: "RemoveTestMethodPragmaWarningDirective"),
            diagnostic);
    }

    private static void RegisterMoveHelperMethodFix(
        CodeFixContext context,
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol,
        SyntaxNode syntaxRoot,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        if (!methodSymbol.IsStatic
            || methodDeclaration.Parent is not TypeDeclarationSyntax typeDeclaration
            || HasUnsupportedHelperInvocation(typeDeclaration, methodDeclaration, methodSymbol, semanticModel, ct))
        {
            return;
        }

        var helperTypeName = $"{typeDeclaration.Identifier.ValueText}Helpers";
        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Move '{methodSymbol.Name}' to '{helperTypeName}'",
                createChangedDocument: ct => MoveHelperMethod(document, syntaxRoot, typeDeclaration, methodDeclaration, methodSymbol, semanticModel, ct),
                equivalenceKey: $"MoveXunitHelperMethod:{helperTypeName}:{methodSymbol.Name}"),
            context.Diagnostics[0]);
    }

    private static void RegisterRequiredTraitFix(
        CodeFixContext context,
        Document document,
        Diagnostic diagnostic,
        MethodDeclarationSyntax methodDeclaration,
        SyntaxNode syntaxRoot)
    {
        var requiredTraitName = GetNonWhiteSpaceProperty(diagnostic, "TraitName");
        var requiredTraitValue = GetNonWhiteSpaceProperty(diagnostic, "TraitValue");
        if (requiredTraitName is null || requiredTraitValue is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add Trait(\"{requiredTraitName}\", \"{requiredTraitValue}\")",
                createChangedDocument: ct => AddRequiredTrait(document, syntaxRoot, methodDeclaration, requiredTraitName, requiredTraitValue, ct),
                equivalenceKey: $"AddRequiredTrait:{requiredTraitName}:{requiredTraitValue}"),
            diagnostic);
    }

    private static string? GetNonWhiteSpaceProperty(Diagnostic diagnostic, string key)
    {
        return diagnostic.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static Task<Document> AddRequiredTrait(
        Document document,
        SyntaxNode syntaxRoot,
        MethodDeclarationSyntax methodDeclaration,
        string traitName,
        string traitValue,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var traitAttribute = Attribute(ParseName("global::Xunit.Trait"))
            .WithArgumentList(
                AttributeArgumentList(
                    SeparatedList([
                        AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(traitName))),
                        AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(traitValue))),
                    ])));
        var attributeList = AttributeList(SingletonSeparatedList(traitAttribute))
            .WithTrailingTrivia(ElasticCarriageReturnLineFeed)
            .WithAdditionalAnnotations(Formatter.Annotation);
        var updatedMethod = methodDeclaration.AddAttributeLists(attributeList);
        var updatedRoot = syntaxRoot.ReplaceNode(methodDeclaration, updatedMethod);

        return Task.FromResult(document.WithSyntaxRoot(updatedRoot));
    }

    private static Task<Document> MoveHelperMethod(
        Document document,
        SyntaxNode syntaxRoot,
        TypeDeclarationSyntax typeDeclaration,
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var helperTypeName = $"{typeDeclaration.Identifier.ValueText}Helpers";
        var invocations = GetSupportedHelperInvocations(typeDeclaration, methodDeclaration, methodSymbol, semanticModel, ct);
        var trackedType = typeDeclaration.TrackNodes(invocations.Cast<SyntaxNode>().Append(methodDeclaration));
        var trackedInvocations = invocations
            .Select(trackedType.GetCurrentNode)
            .OfType<InvocationExpressionSyntax>()
            .ToArray();
        var qualifiedType = QualifyHelperInvocations(trackedType, trackedInvocations, methodSymbol.Name, helperTypeName);
        var methodToMove = qualifiedType.GetCurrentNode(methodDeclaration)!;
        var movedMethod = methodToMove
            .WithModifiers(MakePublicStaticModifiers(methodDeclaration.Modifiers))
            .WithAdditionalAnnotations(Formatter.Annotation);
        var typeWithoutMethod = qualifiedType.RemoveNode(methodToMove, SyntaxRemoveOptions.KeepNoTrivia)!;
        var nestedHelper = FindNestedHelperType(typeWithoutMethod, helperTypeName);

        TypeDeclarationSyntax updatedType;
        if (nestedHelper is null)
        {
            var helperType = ClassDeclaration(helperTypeName)
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword)))
                .WithMembers(List<MemberDeclarationSyntax>([movedMethod]))
                .WithAdditionalAnnotations(Formatter.Annotation);

            updatedType = typeWithoutMethod.AddMembers(helperType);
        }
        else
        {
            var updatedHelper = ContainsMethodNamed(nestedHelper, methodSymbol.Name)
                ? nestedHelper
                : nestedHelper.AddMembers(movedMethod);
            updatedType = typeWithoutMethod.ReplaceNode(nestedHelper, updatedHelper);
        }

        var updatedRoot = syntaxRoot.ReplaceNode(typeDeclaration, updatedType);

        return Task.FromResult(document.WithSyntaxRoot(updatedRoot));
    }

    private static Task<Document> RemovePragmaDirective(
        Document document,
        SyntaxNode syntaxRoot,
        SyntaxTrivia trivia)
    {
        var updatedRoot = syntaxRoot.ReplaceTrivia(trivia, default(SyntaxTrivia));
        return Task.FromResult(document.WithSyntaxRoot(updatedRoot));
    }

    private static SyntaxTokenList MakePublicStaticModifiers(SyntaxTokenList modifiers)
    {
        var tokens = modifiers
            .Where(static token => !token.IsKind(SyntaxKind.PrivateKeyword) && !token.IsKind(SyntaxKind.InternalKeyword))
            .ToList();

        if (!tokens.Any(static token => token.IsKind(SyntaxKind.PublicKeyword)))
        {
            tokens.Insert(0, Token(SyntaxKind.PublicKeyword));
        }

        return TokenList(tokens);
    }

    private static ClassDeclarationSyntax? FindNestedHelperType(TypeDeclarationSyntax typeDeclaration, string helperTypeName)
    {
        return typeDeclaration.Members
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(candidate => string.Equals(candidate.Identifier.ValueText, helperTypeName, StringComparison.Ordinal));
    }

    private static bool ContainsMethodNamed(ClassDeclarationSyntax typeDeclaration, string methodName)
    {
        return typeDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(candidate => string.Equals(candidate.Identifier.ValueText, methodName, StringComparison.Ordinal));
    }

    private static bool HasUnsupportedHelperInvocation(
        TypeDeclarationSyntax typeDeclaration,
        MethodDeclarationSyntax movedMethod,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        foreach (var invocation in typeDeclaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (movedMethod.Span.Contains(invocation.SpanStart)
                || semanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol invokedMethod
                || !SymbolEqualityComparer.Default.Equals(invokedMethod.OriginalDefinition, methodSymbol.OriginalDefinition))
            {
                continue;
            }

            if (invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>() != typeDeclaration
                || invocation.Expression is not IdentifierNameSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static TypeDeclarationSyntax QualifyHelperInvocations(
        TypeDeclarationSyntax typeDeclaration,
        InvocationExpressionSyntax[] invocations,
        string methodName,
        string helperTypeName)
    {
        return typeDeclaration.ReplaceNodes(
            invocations.Where(invocation => invocation.Expression is IdentifierNameSyntax identifier
                && string.Equals(identifier.Identifier.ValueText, methodName, StringComparison.Ordinal)),
            (_, invocation) => invocation.WithExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(helperTypeName),
                    IdentifierName(methodName))));
    }

    private static ImmutableArray<InvocationExpressionSyntax> GetSupportedHelperInvocations(
        TypeDeclarationSyntax typeDeclaration,
        MethodDeclarationSyntax movedMethod,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        var invocations = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();
        foreach (var invocation in typeDeclaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (movedMethod.Span.Contains(invocation.SpanStart)
                || invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>() != typeDeclaration
                || invocation.Expression is not IdentifierNameSyntax
                || semanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol invokedMethod
                || !SymbolEqualityComparer.Default.Equals(invokedMethod.OriginalDefinition, methodSymbol.OriginalDefinition))
            {
                continue;
            }

            invocations.Add(invocation);
        }

        return invocations.ToImmutable();
    }

    private static Task<Solution> RenameSymbolAsync(Solution solution, IMethodSymbol methodSymbol, string targetName, CancellationToken ct)
    {
        return Renamer.RenameSymbolAsync(solution, methodSymbol, new SymbolRenameOptions(), targetName, ct);
    }

    private static bool HasRenameConflict(
        SemanticModel semanticModel,
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol,
        string targetName)
    {
        return semanticModel.LookupSymbols(methodDeclaration.Identifier.SpanStart, name: targetName)
            .Any(candidate => !SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, methodSymbol.OriginalDefinition));
    }

    private static string? TryConvertToUnderscoreName(string methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return null;
        }

        var tokens = Regex.Matches(
                methodName,
                @"[A-Z]+[0-9]*(?=$|[A-Z][a-z])|[A-Z]?[a-z0-9]+",
                RegexOptions.CultureInvariant,
                RegexTimeout)
            .Cast<Match>()
            .Select(static match => match.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (tokens.Length < 2)
        {
            return null;
        }

        return string.Join("_", tokens.Select(NormalizeToken));
    }

    private static string NormalizeToken(string token, int index)
    {
        if (token.All(char.IsDigit))
        {
            return token;
        }

        if (index == 0)
        {
            var characters = token.ToCharArray();
            characters[0] = char.ToUpperInvariant(characters[0]);

            for (var i = 1; i < characters.Length; i++)
            {
                characters[i] = char.ToLowerInvariant(characters[i]);
            }

            return new string(characters);
        }

        var lowercaseCharacters = token.ToCharArray();
        for (var i = 0; i < lowercaseCharacters.Length; i++)
        {
            lowercaseCharacters[i] = char.ToLowerInvariant(lowercaseCharacters[i]);
        }

        return new string(lowercaseCharacters);
    }
}
