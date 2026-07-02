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
    private const string ContainsAssertionName = "Contains";
    private const string EqualAssertionName = "Equal";
    private const string SingleAssertionName = "Single";

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
                title: "Use Should assertion wrapper",
                createChangedDocument: _ => UseShouldAssertionWrapper(document, syntaxRoot, memberAccess),
                equivalenceKey: equivalenceKey),
            diagnostic);
    }

    private static bool CanRewriteXunitAssertion(MemberAccessExpressionSyntax memberAccess, out string equivalenceKey)
    {
        var name = memberAccess.Name.Identifier.ValueText;
        equivalenceKey = $"UseTestAssertWrapper:{name}";

        return name switch
        {
            EqualAssertionName => CanRewriteEqualInvocation(memberAccess),
            SingleAssertionName => CanRewriteSingleInvocation(memberAccess),
            "All"
            or ContainsAssertionName
            or "DoesNotContain"
            or "Empty"
            or "False"
            or "InRange"
            or "NotEmpty"
            or "NotEqual"
            or "NotNull"
            or "Null"
            or "Same"
            or "True" => true,
            _ => false,
        };
    }

    private static bool CanRewriteEqualInvocation(MemberAccessExpressionSyntax memberAccess)
    {
        return memberAccess.FirstAncestorOrSelf<InvocationExpressionSyntax>() is { ArgumentList.Arguments: var arguments }
            && (arguments.Count == 2
                || (arguments.Count == 3 && arguments.Any(IsIgnoreCaseArgument)));
    }

    private static bool CanRewriteSingleInvocation(MemberAccessExpressionSyntax memberAccess)
    {
        return memberAccess.FirstAncestorOrSelf<InvocationExpressionSyntax>() is { ArgumentList.Arguments.Count: 1 };
    }

    private static Task<Document> UseShouldAssertionWrapper(
        Document document,
        SyntaxNode syntaxRoot,
        MemberAccessExpressionSyntax memberAccess)
    {
        var invocation = memberAccess.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null || !TryCreateShouldAssertionInvocation(invocation, memberAccess, out var updatedInvocation))
        {
            return Task.FromResult(document);
        }

        var updatedRoot = syntaxRoot.ReplaceNode(invocation, updatedInvocation);

        return Task.FromResult(document.WithSyntaxRoot(AddAssertionUsing(updatedRoot)));
    }

    private static bool TryCreateShouldAssertionInvocation(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        out InvocationExpressionSyntax updatedInvocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        var assertionName = memberAccess.Name.Identifier.ValueText;
        updatedInvocation = invocation;

        return assertionName switch
        {
            "All" when arguments.Count == 2 => TryCreateShouldInvocation(arguments[0], "ShouldAllSatisfy", [arguments[1]], out updatedInvocation),
            ContainsAssertionName when arguments.Count == 2 && arguments[1].Expression is ParenthesizedLambdaExpressionSyntax or SimpleLambdaExpressionSyntax => TryCreateShouldInvocation(arguments[0], "ShouldContain", [arguments[1]], out updatedInvocation),
            ContainsAssertionName when arguments.Count == 2 => TryCreateShouldInvocation(arguments[1], "ShouldContain", [arguments[0]], out updatedInvocation),
            ContainsAssertionName when arguments.Count == 3 => TryCreateShouldInvocation(arguments[1], "ShouldContain", [arguments[0], arguments[2]], out updatedInvocation),
            "DoesNotContain" when arguments.Count == 2 => TryCreateShouldInvocation(arguments[1], "ShouldNotContain", [arguments[0]], out updatedInvocation),
            "Empty" when arguments.Count == 1 => TryCreateShouldInvocation(arguments[0], "ShouldBeEmpty", [], out updatedInvocation),
            EqualAssertionName when arguments.Count == 2 => TryCreateShouldInvocation(arguments[1], "ShouldBe", [arguments[0]], out updatedInvocation),
            EqualAssertionName when arguments.Count == 3 && TryGetIgnoreCaseComparer(arguments, out var comparerExpression) => TryCreateShouldInvocation(arguments[1], "ShouldBe", [arguments[0], Argument(ParseExpression(comparerExpression))], out updatedInvocation),
            "False" when arguments.Count == 1 => TryCreateShouldInvocation(arguments[0], "ShouldBeFalse", [], out updatedInvocation),
            "False" when arguments.Count == 2 => TryCreateShouldInvocation(arguments[0], "ShouldBeFalse", [arguments[1]], out updatedInvocation),
            "InRange" when arguments.Count == 3 => TryCreateShouldInvocation(arguments[0], "ShouldBeInRange", [arguments[1], arguments[2]], out updatedInvocation),
            "NotEmpty" when arguments.Count == 1 => TryCreateShouldInvocation(arguments[0], "ShouldNotBeEmpty", [], out updatedInvocation),
            "NotEqual" when arguments.Count == 2 => TryCreateShouldInvocation(arguments[1], "ShouldNotBe", [arguments[0]], out updatedInvocation),
            "NotEqual" when arguments.Count == 3 => TryCreateShouldInvocation(arguments[1], "ShouldNotBe", [arguments[0], arguments[2]], out updatedInvocation),
            "NotNull" when arguments.Count == 1 => TryCreateShouldInvocation(arguments[0], "ShouldNotBeNull", [], out updatedInvocation),
            "Null" when arguments.Count == 1 => TryCreateShouldInvocation(arguments[0], "ShouldBeNull", [], out updatedInvocation),
            "Same" when arguments.Count == 2 => TryCreateShouldInvocation(arguments[1], "ShouldBeSameAs", [arguments[0]], out updatedInvocation),
            "Single" when arguments.Count == 1 => TryCreateShouldInvocation(arguments[0], "ShouldHaveSingleItem", [], out updatedInvocation),
            "True" when arguments.Count == 1 => TryCreateShouldInvocation(arguments[0], "ShouldBeTrue", [], out updatedInvocation),
            "True" when arguments.Count == 2 => TryCreateShouldInvocation(arguments[0], "ShouldBeTrue", [arguments[1]], out updatedInvocation),
            _ => false,
        };
    }

    private static bool TryCreateShouldInvocation(
        ArgumentSyntax receiverArgument,
        string shouldMethodName,
        IEnumerable<ArgumentSyntax> arguments,
        out InvocationExpressionSyntax invocation)
    {
        invocation = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ParenthesizedExpression(receiverArgument.Expression.WithoutTrivia()),
                    IdentifierName(shouldMethodName)))
            .WithArgumentList(ArgumentList(SeparatedList(arguments.Select(static argument => argument.WithNameColon(null)))))
            .WithTriviaFrom(receiverArgument.Expression)
            .WithAdditionalAnnotations(Formatter.Annotation);

        return true;
    }

    private static SyntaxNode AddAssertionUsing(SyntaxNode syntaxRoot)
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

    private static bool TryGetIgnoreCaseComparer(SeparatedSyntaxList<ArgumentSyntax> arguments, out string comparerExpression)
    {
        var ignoreCaseArgument = arguments.FirstOrDefault(IsIgnoreCaseArgument);
        comparerExpression = "System.StringComparer.Ordinal";
        if (ignoreCaseArgument is not { RawKind: not 0 })
        {
            return false;
        }

        comparerExpression = ignoreCaseArgument.Expression switch
        {
            LiteralExpressionSyntax literalExpression when literalExpression.IsKind(SyntaxKind.TrueLiteralExpression) => "System.StringComparer.OrdinalIgnoreCase",
            LiteralExpressionSyntax literalExpression when literalExpression.IsKind(SyntaxKind.FalseLiteralExpression) => "System.StringComparer.Ordinal",
            _ => $"({ignoreCaseArgument.Expression}) ? System.StringComparer.OrdinalIgnoreCase : System.StringComparer.Ordinal",
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
