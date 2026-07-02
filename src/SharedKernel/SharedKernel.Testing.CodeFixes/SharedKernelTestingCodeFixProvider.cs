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
        [TestingDiagnosticIds.TestMethodWarningSuppression, TestingDiagnosticIds.XunitTestMethodNaming, TestingDiagnosticIds.XunitTestMethodRequiredTrait, TestingDiagnosticIds.XunitSerialCollectionJustification, TestingDiagnosticIds.XunitAssertionWrapper];

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

        if (string.Equals(diagnostic.Id, TestingDiagnosticIds.XunitSerialCollectionJustification, StringComparison.Ordinal))
        {
            RegisterSerialJustificationFix(context, document, diagnostic, syntaxRoot);
            return;
        }

        if (string.Equals(diagnostic.Id, TestingDiagnosticIds.XunitAssertionWrapper, StringComparison.Ordinal))
        {
            RegisterXunitAssertionWrapperFix(context, document, diagnostic, syntaxRoot);
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

    private static void RegisterXunitAssertionWrapperFix(
        CodeFixContext context,
        Document document,
        Diagnostic diagnostic,
        SyntaxNode syntaxRoot)
    {
        if (syntaxRoot.FindNode(context.Span).FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { Expression: MemberAccessExpressionSyntax memberAccess })
        {
            return;
        }

        if (!CanRewriteXunitAssertion(memberAccess, out var equivalenceKey))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Use TestAssert wrapper",
                createChangedDocument: _ => UseTestAssertWrapper(document, syntaxRoot, memberAccess),
                equivalenceKey: equivalenceKey),
            diagnostic);
    }

    private static bool CanRewriteXunitAssertion(MemberAccessExpressionSyntax memberAccess, out string equivalenceKey)
    {
        var name = memberAccess.Name.Identifier.ValueText;
        equivalenceKey = $"UseTestAssertWrapper:{name}";

        return name is "All"
            or "Collection"
            or "Contains"
            or "DoesNotContain"
            or "Empty"
            or "Equal"
            or "False"
            or "InRange"
            or "IsAssignableFrom"
            or "IsNotAssignableFrom"
            or "IsNotType"
            or "IsType"
            or "NotEmpty"
            or "NotEqual"
            or "NotInRange"
            or "NotNull"
            or "Null"
            or "Same"
            or "Single"
            or "Throws"
            or "True";
    }

    private static Task<Document> UseTestAssertWrapper(
        Document document,
        SyntaxNode syntaxRoot,
        MemberAccessExpressionSyntax memberAccess)
    {
        var invocation = memberAccess.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        var targetName = memberAccess.Name.Identifier.ValueText is "Single"
            ? "ExactlyOne"
            : memberAccess.Name.Identifier.ValueText;
        var testAssert = IdentifierName("TestAssert")
            .WithTriviaFrom(memberAccess.Expression)
            .WithAdditionalAnnotations(Formatter.Annotation);
        var updatedMemberAccess = memberAccess
            .WithExpression(testAssert)
            .WithName(IdentifierName(targetName).WithTriviaFrom(memberAccess.Name));

        var updatedRoot = invocation is { ArgumentList.Arguments: var arguments }
            && string.Equals(targetName, "Equal", StringComparison.Ordinal)
            && TryGetIgnoreCaseComparer(arguments, out _)
            ? ReplaceEqualIgnoreCaseInvocation(syntaxRoot, invocation, updatedMemberAccess)
            : syntaxRoot.ReplaceNode(memberAccess, updatedMemberAccess);

        return Task.FromResult(document.WithSyntaxRoot(AddTestAssertUsing(updatedRoot)));
    }

    private static SyntaxNode AddTestAssertUsing(SyntaxNode syntaxRoot)
    {
        if (syntaxRoot is not CompilationUnitSyntax compilationUnit
            || compilationUnit.Usings.Any(static usingDirective => string.Equals(
                usingDirective.Name?.ToString(),
                "SharedKernel.Testing.Assertions",
                StringComparison.Ordinal)))
        {
            return syntaxRoot;
        }

        var usingDirective = UsingDirective(ParseName("SharedKernel.Testing.Assertions"));
        return compilationUnit.AddUsings(usingDirective)
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static SyntaxNode ReplaceEqualIgnoreCaseInvocation(
        SyntaxNode syntaxRoot,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax updatedMemberAccess)
    {
        _ = TryGetIgnoreCaseComparer(invocation.ArgumentList.Arguments, out var comparerExpression);
        var comparerArgument = Argument(ParseExpression(comparerExpression));
        var updatedArguments = SeparatedList(invocation.ArgumentList.Arguments
            .Where(static argument => !IsIgnoreCaseArgument(argument))
            .Append(comparerArgument));
        var updatedInvocation = invocation
            .WithExpression(updatedMemberAccess)
            .WithArgumentList(invocation.ArgumentList.WithArguments(updatedArguments));

        return syntaxRoot.ReplaceNode(invocation, updatedInvocation);
    }

    private static bool TryGetIgnoreCaseComparer(SeparatedSyntaxList<ArgumentSyntax> arguments, out string comparerExpression)
    {
        var ignoreCaseArgument = arguments.FirstOrDefault(IsIgnoreCaseArgument);
        comparerExpression = "StringComparer.Ordinal";
        if (ignoreCaseArgument is not { RawKind: not 0 })
        {
            return false;
        }

        comparerExpression = ignoreCaseArgument.Expression switch
        {
            LiteralExpressionSyntax literalExpression when literalExpression.IsKind(SyntaxKind.TrueLiteralExpression) => "StringComparer.OrdinalIgnoreCase",
            LiteralExpressionSyntax literalExpression when literalExpression.IsKind(SyntaxKind.FalseLiteralExpression) => "StringComparer.Ordinal",
            _ => $"({ignoreCaseArgument.Expression}) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal",
        };
        return true;
    }

    private static bool IsIgnoreCaseArgument(ArgumentSyntax argument)
    {
        return string.Equals(argument.NameColon?.Name.Identifier.ValueText, "ignoreCase", StringComparison.Ordinal);
    }

    private static void RegisterSerialJustificationFix(
        CodeFixContext context,
        Document document,
        Diagnostic diagnostic,
        SyntaxNode syntaxRoot)
    {
        if (syntaxRoot.FindNode(context.Span).FirstAncestorOrSelf<TypeDeclarationSyntax>() is not TypeDeclarationSyntax typeDeclaration)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add SerialTestJustification attribute",
                createChangedDocument: ct => AddSerialJustification(document, syntaxRoot, typeDeclaration, ct),
                equivalenceKey: "AddSerialTestJustification"),
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

    private static Task<Document> RemovePragmaDirective(
        Document document,
        SyntaxNode syntaxRoot,
        SyntaxTrivia trivia)
    {
        var updatedRoot = syntaxRoot.ReplaceTrivia(trivia, default(SyntaxTrivia));
        return Task.FromResult(document.WithSyntaxRoot(updatedRoot));
    }

    private static Task<Document> AddSerialJustification(
        Document document,
        SyntaxNode syntaxRoot,
        TypeDeclarationSyntax typeDeclaration,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var justificationAttribute = Attribute(ParseName("global::SharedKernel.Testing.SerialTestJustification"))
            .WithArgumentList(
                AttributeArgumentList(
                    SingletonSeparatedList(
                        AttributeArgument(
                            LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                Literal("TODO: explain why this collection must run serially."))))));
        var attributeList = AttributeList(SingletonSeparatedList(justificationAttribute))
            .WithTrailingTrivia(ElasticCarriageReturnLineFeed)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var collectionDefinitionIndex = typeDeclaration.AttributeLists.IndexOf(
            typeDeclaration.AttributeLists.FirstOrDefault(attributeList => attributeList.ToString().Contains("CollectionDefinition", StringComparison.Ordinal)));
        if (collectionDefinitionIndex < 0)
        {
            return Task.FromResult(document);
        }

        var collectionDefinitionAttributeList = typeDeclaration.AttributeLists[collectionDefinitionIndex];
        var updatedAttributeList = attributeList.WithLeadingTrivia(collectionDefinitionAttributeList.GetLeadingTrivia());
        var updatedCollectionDefinitionAttributeList = collectionDefinitionAttributeList.WithLeadingTrivia();
        var updatedAttributeLists = typeDeclaration.AttributeLists
            .Replace(collectionDefinitionAttributeList, updatedCollectionDefinitionAttributeList)
            .Insert(collectionDefinitionIndex, updatedAttributeList);
        var updatedType = typeDeclaration.WithAttributeLists(updatedAttributeLists);
        var updatedRoot = syntaxRoot.ReplaceNode(typeDeclaration, updatedType);

        return Task.FromResult(document.WithSyntaxRoot(updatedRoot));
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
