using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.OpenApi;

/// <summary>
/// Adds reusable OpenAPI document registration helpers for boundary-specific API artifacts.
/// </summary>
public static class OpenApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default OpenAPI document and one named document per boundary definition.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="boundaryDocuments">The boundary-specific document definitions to register.</param>
    public static void AddBoundaryOpenApiDocuments(
        this IServiceCollection services,
        IReadOnlyCollection<OpenApiBoundaryDocument> boundaryDocuments)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(boundaryDocuments);

        services.AddOpenApi();

        foreach (var boundaryDocument in boundaryDocuments)
        {
            services.AddOpenApi(boundaryDocument.DocumentName, options =>
                options.ShouldInclude = description =>
                    description.RelativePath?.StartsWith(boundaryDocument.RoutePrefix, StringComparison.OrdinalIgnoreCase) == true);
        }
    }
}
