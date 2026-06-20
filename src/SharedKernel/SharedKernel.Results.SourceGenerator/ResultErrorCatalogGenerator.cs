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
        var entries = context.CompilationProvider
            .Select(static (compilation, cancellationToken) => ErrorCatalogModelBuilder.Build(compilation, cancellationToken))
            .WithTrackingName("ResultErrorCatalogEntries");

        context.RegisterSourceOutput(
            entries,
            static (productionContext, generatedEntries) =>
            {
                productionContext.AddSource(
                    GeneratedHintNames.ResultErrorCatalog,
                    SourceText.From(ErrorCatalogEmitter.Emit([.. generatedEntries]), Encoding.UTF8));
            });
    }
}
