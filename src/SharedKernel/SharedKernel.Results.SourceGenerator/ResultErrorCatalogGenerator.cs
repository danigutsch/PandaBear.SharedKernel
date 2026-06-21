using System.Text;
using Microsoft.CodeAnalysis;
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
        var entries = context.CompilationProvider
            .Select(static (compilation, cancellationToken) => ErrorCatalogModelBuilder.Build(compilation, cancellationToken))
            .WithTrackingName("ResultErrorCatalogEntries");
        var generationInput = providerTypeName.Combine(entries)
            .WithTrackingName("ResultErrorCatalogGenerationInput");

        context.RegisterSourceOutput(
            generationInput,
            static (productionContext, input) =>
            {
                if (input.Right.Length == 0)
                {
                    return;
                }

                productionContext.AddSource(
                    GeneratedHintNames.ResultErrorCatalog,
                    SourceText.From(ErrorCatalogEmitter.Emit(input.Left, [.. input.Right]), Encoding.UTF8));
            });
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
