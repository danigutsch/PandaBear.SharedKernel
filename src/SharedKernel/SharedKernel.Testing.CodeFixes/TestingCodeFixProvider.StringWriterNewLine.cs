using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SharedKernel.Testing.CodeFixes;

public sealed partial class TestingCodeFixProvider
{
    private const string InvariantStringWriterNamespace = "SharedKernel.Testing";
    private const string InvariantStringWriterName = InvariantStringWriterNamespace + ".InvariantStringWriter";

    // Rewrites new StringWriter() and new StringWriter(CultureInfo.InvariantCulture) to new InvariantStringWriter(), keeping any
    // object initializer. A writer over a caller's StringBuilder or with another format provider keeps the diagnostic without a fix,
    // because the replacement would drop the builder or change how values are formatted.
    private static async Task RegisterStringWriterNewLineFix(CodeFixContext context, Document document, Diagnostic diagnostic, SyntaxNode root)
    {
        var creation = root.FindNode(context.Span, getInnermostNodeForTie: true).FirstAncestorOrSelf<BaseObjectCreationExpressionSyntax>();
        if (creation is null)
        {
            return;
        }

        var model = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (model?.Compilation.GetTypeByMetadataName(InvariantStringWriterName) is null
            || !HasReplaceableStringWriterArguments(model, creation, context.CancellationToken))
        {
            return;
        }

        var replacement = ObjectCreationExpression(ParseTypeName(InvariantStringWriterName).WithAdditionalAnnotations(Simplifier.Annotation))
            .WithArgumentList(ArgumentList())
            .WithInitializer(creation.Initializer)
            .WithTriviaFrom(creation)
            .WithAdditionalAnnotations(Formatter.Annotation);
        var updatedRoot = AddNamespaceUsing(root.ReplaceNode(creation, replacement), InvariantStringWriterNamespace);

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use InvariantStringWriter",
                async ct =>
                {
                    var simplified = await Simplifier.ReduceAsync(document.WithSyntaxRoot(updatedRoot), Simplifier.Annotation, cancellationToken: ct)
                        .ConfigureAwait(false);
                    return await Formatter.FormatAsync(simplified, Formatter.Annotation, cancellationToken: ct).ConfigureAwait(false);
                },
                equivalenceKey: "UseInvariantStringWriter"),
            diagnostic);
    }

    private static bool HasReplaceableStringWriterArguments(SemanticModel model, BaseObjectCreationExpressionSyntax creation, CancellationToken ct)
    {
        var arguments = creation.ArgumentList?.Arguments ?? default;
        if (arguments.Count == 0)
        {
            return true;
        }

        return arguments.Count == 1
            && model.GetSymbolInfo(arguments[0].Expression, ct).Symbol is IPropertySymbol { Name: "InvariantCulture" } property
            && string.Equals(property.ContainingType.ToDisplayString(), "System.Globalization.CultureInfo", StringComparison.Ordinal);
    }
}
