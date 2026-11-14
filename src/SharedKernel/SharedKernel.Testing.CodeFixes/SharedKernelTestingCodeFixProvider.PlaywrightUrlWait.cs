using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace PandaBear.Testing.CodeFixes;

public sealed partial class SharedKernelTestingCodeFixProvider
{
    private const string ExpectPathName = "ExpectPath";
    private const string ExpectPathNamespace = "PandaBear.Testing.Playwright";
    private const string ExpectPathContainer = ExpectPathNamespace + ".PlaywrightPageExtensions";

    // Characters with a meaning in a Playwright URL glob; a string with any of them is a pattern, not an address. A query's '?' is among
    // them, so an address with a query is left for a person to rewrite.
    private static readonly char[] UrlGlobCharacters = ['*', '?', '{', '}', '[', ']'];

    // Rewrites page.WaitForURLAsync(address) to page.ExpectPath(new Uri(address)) only when the argument is certainly one exact absolute
    // address: a glob, a predicate, a regular expression, an options argument or a frame keeps the diagnostic without a fix.
    private static async Task RegisterPlaywrightUrlWaitFix(CodeFixContext context, Document document, Diagnostic diagnostic, SyntaxNode root)
    {
        var invocation = root.FindNode(context.Span).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is not { Expression: MemberAccessExpressionSyntax member }
            || invocation.ArgumentList.Arguments.Count != 1
            || invocation.ArgumentList.Arguments[0].NameColon is not null)
        {
            return;
        }

        var model = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model?.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !string.Equals(method.ContainingType.ToDisplayString(), "Microsoft.Playwright.IPage", StringComparison.Ordinal)
            || method.Parameters[0].Type.SpecialType != SpecialType.System_String
            || !HasExpectPath(model.Compilation))
        {
            return;
        }

        var address = GetExactAddress(model, invocation.ArgumentList.Arguments[0].Expression, context.CancellationToken);
        if (address is null)
        {
            return;
        }

        var replacement = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, member.Expression, IdentifierName(ExpectPathName)),
                ArgumentList(SingletonSeparatedList(Argument(address))))
            .WithTriviaFrom(invocation)
            .WithAdditionalAnnotations(Formatter.Annotation);
        var updatedRoot = AddNamespaceUsing(root.ReplaceNode(invocation, replacement), ExpectPathNamespace);

        context.RegisterCodeFix(
            CodeAction.Create(
                "Wait with ExpectPath",
                async ct =>
                {
                    var simplified = await Simplifier.ReduceAsync(document.WithSyntaxRoot(updatedRoot), Simplifier.Annotation, cancellationToken: ct)
                        .ConfigureAwait(false);
                    return await Formatter.FormatAsync(simplified, Formatter.Annotation, cancellationToken: ct).ConfigureAwait(false);
                },
                equivalenceKey: "UseExpectPath"),
            diagnostic);
    }

    private static bool HasExpectPath(Compilation compilation)
    {
        var uri = compilation.GetTypeByMetadataName("System.Uri");
        return compilation.GetTypeByMetadataName(ExpectPathContainer) is { } extensions
            && extensions.GetMembers(ExpectPathName).OfType<IMethodSymbol>().Any(candidate =>
                candidate.IsExtensionMethod
                && candidate.Parameters.Length == 2
                && SymbolEqualityComparer.Default.Equals(candidate.Parameters[1].Type, uri));
    }

    // The Uri for the argument: the receiver of uri.ToString(), or new Uri(argument) for a constant absolute http(s) address or an
    // interpolated string that starts with a literal http(s) scheme and whose text holds no glob character. Null for anything else,
    // such as a relative address or a variable that may hold a pattern.
    private static ExpressionSyntax? GetExactAddress(SemanticModel model, ExpressionSyntax argument, CancellationToken ct)
    {
        if (argument is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ToString" } toString,
                ArgumentList.Arguments.Count: 0,
            }
            && model.GetTypeInfo(toString.Expression, ct).Type?.ToDisplayString() == "System.Uri")
        {
            return toString.Expression.WithoutTrivia();
        }

        var exact = model.GetConstantValue(argument, ct) is { HasValue: true, Value: string constant }
            ? constant.IndexOfAny(UrlGlobCharacters) < 0
                && Uri.TryCreate(constant, UriKind.Absolute, out var address)
                && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps)
                && StartsWithHttpScheme(constant)
            : argument is InterpolatedStringExpressionSyntax interpolated
                && interpolated.Contents.FirstOrDefault() is InterpolatedStringTextSyntax first
                && StartsWithHttpScheme(first.TextToken.ValueText)
                && interpolated.Contents.OfType<InterpolatedStringTextSyntax>()
                    .All(static text => text.TextToken.ValueText.IndexOfAny(UrlGlobCharacters) < 0);
        if (!exact)
        {
            return null;
        }

        return ObjectCreationExpression(ParseTypeName("System.Uri").WithAdditionalAnnotations(Simplifier.Annotation))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(argument.WithoutTrivia()))));
    }

    // An http or https address written out in full. A root-relative "/customers" is absolute on Unix (a file: URI), so the scheme must
    // be checked; an interpolated string qualifies only when its literal start names the scheme.
    private static bool StartsWithHttpScheme(string text)
    {
        return text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static SyntaxNode AddNamespaceUsing(SyntaxNode syntaxRoot, string namespaceName)
    {
        if (syntaxRoot is not CompilationUnitSyntax compilationUnit
            || compilationUnit.Usings.Any(usingDirective => string.Equals(usingDirective.Name?.ToString(), namespaceName, StringComparison.Ordinal)))
        {
            return syntaxRoot;
        }

        return compilationUnit.AddUsings(UsingDirective(ParseName(namespaceName))).WithAdditionalAnnotations(Formatter.Annotation);
    }
}
