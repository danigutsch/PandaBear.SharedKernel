using Microsoft.AspNetCore.OpenApi;

namespace PandaBear.OpenApi.Tests;

/// <summary>
/// Stores the runtime OpenAPI transformer context for test reuse.
/// </summary>
internal sealed class OpenApiContextCapture
{
    /// <summary>
    /// Gets or sets the most recent transformer context.
    /// </summary>
    public OpenApiDocumentTransformerContext? Context
    {
        get; set;
    }
}
