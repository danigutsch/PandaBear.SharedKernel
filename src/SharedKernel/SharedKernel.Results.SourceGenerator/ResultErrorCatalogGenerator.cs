using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.Results.SourceGenerator;

/// <summary>
/// Discovers centralized <c>*Errors</c> classes and emits a generated error catalog for the current assembly.
/// </summary>
[Generator]
public sealed class ResultErrorCatalogGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var providerTypeName = context.CompilationProvider
            .Select(static (compilation, _) => SanitizeProviderTypeName(compilation.AssemblyName ?? "GeneratedResultErrorCatalogProvider"))
            .WithTrackingName("ResultErrorCatalogProviderTypeName");
        var entries = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsCandidateErrorProvider(node),
                static (syntaxContext, cancellationToken) => ErrorCatalogModelBuilder.Build(
                    (ClassDeclarationSyntax)syntaxContext.Node,
                    syntaxContext.SemanticModel,
                    cancellationToken))
            .Collect()
            .Select(static (providerEntries, _) => providerEntries
                .SelectMany(static entry => entry)
                .OrderBy(static entry => entry.Identifier, StringComparer.Ordinal)
                .ToImmutableArray())
            .Select(static (entries, _) => new ErrorCatalogModel(entries))
            .WithTrackingName("ResultErrorCatalogEntries");
        var generationInput = providerTypeName.Combine(entries)
            .WithTrackingName("ResultErrorCatalogGenerationInput");

        context.RegisterSourceOutput(
            generationInput,
            static (productionContext, input) =>
            {
                if (input.Right.Entries.Length == 0)
                {
                    return;
                }

                productionContext.AddSource(
                    GeneratedHintNames.ResultErrorCatalog,
                    SourceText.From(ErrorCatalogEmitter.Emit(input.Left, input.Right.Entries), Encoding.UTF8));
            });
    }

    private static bool IsCandidateErrorProvider(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDeclaration
            && classDeclaration.Identifier.ValueText.EndsWith("Errors", StringComparison.Ordinal)
            && classDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword)
            && classDeclaration.Members.Count > 0;
    }

    internal static string SanitizeProviderTypeName(string assemblyName)
    {
        var builder = new StringBuilder("Generated");

        foreach (var character in assemblyName.Where(char.IsLetterOrDigit))
        {
            builder.Append(character);
        }

        builder.Append("ResultErrorCatalogProvider");
        return builder.ToString();
    }
}
