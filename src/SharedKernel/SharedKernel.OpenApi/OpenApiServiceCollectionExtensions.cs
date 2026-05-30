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
        var registeredBoundaryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var boundaryName in boundaryNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(boundaryName);

            if (!registeredBoundaryNames.Add(boundaryName))
            {
                throw new ArgumentException(
                    $"Duplicate boundary name '{boundaryName}' is not allowed.",
                    nameof(boundaryNames));
            }

            services.AddOpenApi(boundaryName, options =>
            {
                options.AddDocumentTransformer<MultipartFormRequestBodyDocumentTransformer>();
                options.ShouldInclude = description =>
                    description.RelativePath is string relativePath
                    && (string.Equals(relativePath, boundaryName, StringComparison.OrdinalIgnoreCase)
                        || relativePath.StartsWith($"{boundaryName}/", StringComparison.OrdinalIgnoreCase));
            });
        }
    }
}
