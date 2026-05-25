using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.OpenApi;

/// <summary>
/// Adds reusable OpenAPI document registration helpers for boundary-specific API artifacts.
/// </summary>
public static class OpenApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default OpenAPI document and one named document per boundary name.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="boundaryNames">The boundary names whose document names and route prefixes are the same.</param>
    public static void AddBoundaryOpenApiDocuments(
        this IServiceCollection services,
        IReadOnlyCollection<string> boundaryNames)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(boundaryNames);

        services.AddOpenApi();

        foreach (var boundaryName in boundaryNames)
        {
            services.AddOpenApi(boundaryName, options =>
                options.ShouldInclude = description =>
                    description.RelativePath is string relativePath
                    && (string.Equals(relativePath, boundaryName, StringComparison.OrdinalIgnoreCase)
                        || relativePath.StartsWith($"{boundaryName}/", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
