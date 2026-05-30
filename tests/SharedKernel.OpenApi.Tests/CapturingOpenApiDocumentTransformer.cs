using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SharedKernel.OpenApi.Tests;

/// <summary>
/// Captures the ASP.NET Core OpenAPI transformer context during document generation.
/// </summary>
internal sealed class CapturingOpenApiDocumentTransformer(OpenApiContextCapture capture) : IOpenApiDocumentTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        capture.Context = context;
        return Task.CompletedTask;
    }
}
