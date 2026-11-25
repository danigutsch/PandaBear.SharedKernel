using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using PandaBear.AspNetCore.Analyzers;

namespace PandaBear.AspNetCore.CodeFixes;

/// <summary>Adds the missing annotations for a reviewed runtime endpoint binding.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RuntimeEndpointBindingCodeFixProvider))]
public sealed class RuntimeEndpointBindingCodeFixProvider : CodeFixProvider
{
    private const string Title = "Annotate the runtime endpoint binding";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuntimeEndpointDiagnostics.Id];

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        var generator = SyntaxGenerator.GetGenerator(document);
        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            var declaration = node.AncestorsAndSelf()
                .FirstOrDefault(candidate => candidate is MethodDeclarationSyntax or LocalFunctionStatementSyntax);
            if (declaration is null)
            {
                continue;
            }

            var attributes = new List<AttributeSyntax>();
            if (diagnostic.Properties.ContainsKey(RuntimeEndpointDiagnostics.DynamicCode)
                && model.Compilation.GetTypeByMetadataName(RuntimeEndpointDiagnostics.DynamicCode) is { } dynamicCode)
            {
                attributes.Add(CreateAttribute(generator, dynamicCode,
                    "Maps a route handler that relies on runtime-generated code."));
            }

            if (diagnostic.Properties.ContainsKey(RuntimeEndpointDiagnostics.UnreferencedCode)
                && model.Compilation.GetTypeByMetadataName(RuntimeEndpointDiagnostics.UnreferencedCode) is { } unreferencedCode)
            {
                attributes.Add(CreateAttribute(generator, unreferencedCode,
                    "Maps a route handler that requires unreferenced application code."));
            }

            if (attributes.Count > 0)
            {
                context.RegisterCodeFix(CodeAction.Create(Title,
                    _ => Task.FromResult(AddAttributes(document, root, declaration, attributes)), Title), diagnostic);
            }
        }
    }

    private static AttributeSyntax CreateAttribute(SyntaxGenerator generator, INamedTypeSymbol annotation, string message)
    {
        var qualifiedName = ((NameSyntax)generator.TypeExpression(annotation))
            .WithAdditionalAnnotations(Simplifier.Annotation, Simplifier.AddImportsAnnotation);
        return SyntaxFactory.Attribute(qualifiedName,
            SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(message))))));
    }

    private static Document AddAttributes(Document document, SyntaxNode root, SyntaxNode declaration, List<AttributeSyntax> attributes)
    {
        var list = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList(attributes));
        var withoutLeadingTrivia = declaration.WithoutLeadingTrivia();
        SyntaxNode updated = withoutLeadingTrivia is MethodDeclarationSyntax method
            ? method.AddAttributeLists(list)
            : ((LocalFunctionStatementSyntax)withoutLeadingTrivia).AddAttributeLists(list);
        return document.WithSyntaxRoot(root.ReplaceNode(declaration, updated
            .WithLeadingTrivia(declaration.GetLeadingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation)));
    }
}
