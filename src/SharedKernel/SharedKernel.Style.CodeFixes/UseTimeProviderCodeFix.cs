using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace PandaBear.Style.CodeFixes;

internal static class UseTimeProviderCodeFix
{
    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null
            || root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not ExpressionSyntax expression
            || model.GetSymbolInfo(expression, context.CancellationToken).Symbol is not IPropertySymbol property)
        {
            return;
        }

        var suffix = (property.ContainingType.ToDisplayString(), property.Name) switch
        {
            ("System.DateTimeOffset", "UtcNow") => ".GetUtcNow()",
            ("System.DateTimeOffset", "Now") => ".GetLocalNow()",
            ("System.DateTime", "UtcNow") => ".GetUtcNow().UtcDateTime",
            _ => null,
        };
        if (suffix is null)
        {
            return;
        }

        var providers = model.LookupSymbols(expression.SpanStart).Where(static symbol => IsProvider(symbol)).ToArray();
        if (providers.Length != 1)
        {
            return;
        }

        var provider = providers[0];
        var name = SyntaxFacts.GetKeywordKind(provider.Name) == SyntaxKind.None ? provider.Name : "@" + provider.Name;
        var receiver = SyntaxFactory.ParseExpression(name);
        var bound = model.GetSpeculativeSymbolInfo(expression.SpanStart, receiver, SpeculativeBindingOption.BindAsExpression).Symbol;
        if (!SymbolEqualityComparer.Default.Equals(bound, provider))
        {
            return;
        }

        var replacement = SyntaxFactory.ParseExpression(name + suffix).WithTriviaFrom(expression).WithAdditionalAnnotations(Formatter.Annotation);
        var updated = context.Document.WithSyntaxRoot(root.ReplaceNode(expression, replacement));
        var compilation = await updated.Project.GetCompilationAsync(context.CancellationToken).ConfigureAwait(false);
        if (compilation is null || compilation.GetDiagnostics(context.CancellationToken).Any(static d => d.Severity == DiagnosticSeverity.Error))
        {
            return;
        }

        context.RegisterCodeFix(CodeAction.Create("Use the in-scope TimeProvider", _ => Task.FromResult(updated), "UseTimeProvider"), diagnostic);
    }

    private static bool IsProvider(ISymbol symbol)
    {
        var type = symbol switch
        {
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            IFieldSymbol field => field.Type,
            IPropertySymbol { GetMethod: not null, IsIndexer: false } property => property.Type,
            _ => null,
        };
        if (type?.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return false;
        }

        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == "System.TimeProvider")
            {
                return true;
            }
        }

        return false;
    }
}
