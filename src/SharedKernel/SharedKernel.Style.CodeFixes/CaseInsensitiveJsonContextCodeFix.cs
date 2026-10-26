using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace PandaBear.Style.CodeFixes;

/// <summary>
/// Registers the options code fix for <c>SKSTYLE015</c> diagnostics.
/// </summary>
internal static class CaseInsensitiveJsonContextCodeFix
{
    private const string OptionsAttributeName = "JsonSourceGenerationOptions";
    private const string OptionName = "PropertyNameCaseInsensitive";

    /// <summary>
    /// Registers a code action that adds or updates <c>PropertyNameCaseInsensitive = true</c>.
    /// </summary>
    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null
            || root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                .FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } type)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Set PropertyNameCaseInsensitive = true",
                createChangedDocument: _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(type, Fix(type)))),
                equivalenceKey: "CaseInsensitiveJsonContext"),
            diagnostic);
    }

    private static TypeDeclarationSyntax Fix(TypeDeclarationSyntax type)
    {
        var options = type.AttributeLists
            .SelectMany(static list => list.Attributes)
            .FirstOrDefault(static attribute => GetSimpleName(attribute.Name) == OptionsAttributeName);
        if (options is null)
        {
            return AddAttribute(type);
        }

        var trueValue = SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);
        var arguments = options.ArgumentList?.Arguments ?? default;
        var existing = arguments.FirstOrDefault(static argument => argument.NameEquals?.Name.Identifier.ValueText == OptionName);
        AttributeSyntax updated;
        if (existing is not null)
        {
            updated = options.ReplaceNode(existing, existing.WithExpression(trueValue.WithTriviaFrom(existing.Expression)));
        }
        else
        {
            var added = SyntaxFactory.AttributeArgument(trueValue)
                .WithNameEquals(SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName(OptionName)));
            var list = options.ArgumentList is null
                ? SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(added))
                : options.ArgumentList.AddArguments(added);
            updated = options.WithArgumentList(list);
        }

        return type.ReplaceNode(options, updated.WithAdditionalAnnotations(Formatter.Annotation));
    }

    private static TypeDeclarationSyntax AddAttribute(TypeDeclarationSyntax type)
    {
        // Reuse the namespace qualification of a sibling [JsonSerializable] so no new using is required.
        var sibling = type.AttributeLists
            .SelectMany(static list => list.Attributes)
            .Select(static attribute => attribute.Name)
            .FirstOrDefault(static name => GetSimpleName(name) == "JsonSerializable");
        NameSyntax name = sibling is QualifiedNameSyntax qualified
            ? qualified.WithRight(SyntaxFactory.IdentifierName(OptionsAttributeName))
            : SyntaxFactory.IdentifierName(OptionsAttributeName);
        var argument = SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression))
            .WithNameEquals(SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName(OptionName)));
        var attribute = SyntaxFactory.Attribute(name)
            .WithArgumentList(SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(argument)));
        var list = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
            .WithAdditionalAnnotations(Formatter.Annotation);
        if (type.AttributeLists.Count == 0)
        {
            return type.WithAttributeLists(SyntaxFactory.SingletonList(list.WithLeadingTrivia(type.GetLeadingTrivia())))
                .WithLeadingTrivia(SyntaxFactory.ElasticMarker);
        }

        var first = type.AttributeLists[0];
        return type.WithAttributeLists(type.AttributeLists.Insert(0, list.WithLeadingTrivia(first.GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed)));
    }

    private static string GetSimpleName(NameSyntax name)
    {
        var simple = name is QualifiedNameSyntax qualified ? qualified.Right : name;
        var text = simple.ToString();
        return text.EndsWith("Attribute", StringComparison.Ordinal) ? text.Substring(0, text.Length - "Attribute".Length) : text;
    }
}
