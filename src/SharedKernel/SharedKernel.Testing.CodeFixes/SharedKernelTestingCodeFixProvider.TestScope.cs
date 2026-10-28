using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace PandaBear.Testing.CodeFixes;

public sealed partial class SharedKernelTestingCodeFixProvider
{
    private static void RegisterTestScopeFix(
        CodeFixContext context,
        Document document,
        Diagnostic diagnostic,
        MethodDeclarationSyntax methodDeclaration,
        SyntaxNode syntaxRoot)
    {
        // Only a missing scope has an unambiguous fix; picking a value to replace or merge would guess the author's intent.
        if (GetNonWhiteSpaceProperty(diagnostic, "Reason") != "missing"
            || GetNonWhiteSpaceProperty(diagnostic, "AllowedScopes") is not { } allowedScopes
            || methodDeclaration.Parent is not TypeDeclarationSyntax typeDeclaration)
        {
            return;
        }

        var typed = GetNonWhiteSpaceProperty(diagnostic, "TypedAttribute") == "true";
        foreach (var scope in allowedScopes.Split([';'], StringSplitOptions.RemoveEmptyEntries))
        {
            var member = ToEnumMemberName(scope);
            var title = typed ? $"Add TestScope(TestScope.{member})" : $"Add Trait(\"Scope\", \"{scope}\")";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    createChangedDocument: ct => AddTestScope(document, syntaxRoot, typeDeclaration, scope, member, typed, ct),
                    equivalenceKey: $"AddTestScope:{typed}:{scope}"),
                diagnostic);
        }
    }

    private static string ToEnumMemberName(string scope)
    {
        var parts = scope.Split(['-'], StringSplitOptions.RemoveEmptyEntries);

        return string.Concat(parts.Select(static part => char.ToUpper(part[0], CultureInfo.InvariantCulture) + part.Substring(1)));
    }

    private static Task<Document> AddTestScope(
        Document document,
        SyntaxNode syntaxRoot,
        TypeDeclarationSyntax typeDeclaration,
        string scope,
        string member,
        bool typed,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var attribute = typed
            ? Attribute(ParseName("global::PandaBear.Testing.TestScope"))
                .WithArgumentList(
                    AttributeArgumentList(
                        SingletonSeparatedList(
                            AttributeArgument(ParseExpression($"global::PandaBear.Testing.TestScope.{member}")))))
            : Attribute(ParseName("global::Xunit.Trait"))
                .WithArgumentList(
                    AttributeArgumentList(
                        SeparatedList([
                            AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("Scope"))),
                            AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(scope))),
                        ])));
        var attributeList = AttributeList(SingletonSeparatedList(attribute))
            .WithTrailingTrivia(ElasticCarriageReturnLineFeed)
            .WithAdditionalAnnotations(Formatter.Annotation);
        var updatedType = typeDeclaration.AddAttributeLists(attributeList);

        return Task.FromResult(document.WithSyntaxRoot(syntaxRoot.ReplaceNode(typeDeclaration, updatedType)));
    }
}
