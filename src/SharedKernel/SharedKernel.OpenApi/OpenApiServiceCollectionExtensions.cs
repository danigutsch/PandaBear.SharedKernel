using Microsoft.Extensions.DependencyInjection;
using SharedKernel.ApiVersioning;

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

    /// <summary>
    /// Registers one named OpenAPI document per API contract version.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="versions">The API contract versions to register.</param>
    /// <param name="routePrefix">The unversioned API route prefix.</param>
    public static void AddApiVersionOpenApiDocuments(
        this IServiceCollection services,
        IReadOnlyCollection<ApiVersionDefinition> versions,
        string routePrefix = "api")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentException.ThrowIfNullOrWhiteSpace(routePrefix);

        services.AddOpenApi();
        var registeredDocumentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prefix = routePrefix.Trim('/');

        foreach (ApiVersionDefinition version in versions)
        {
            ArgumentNullException.ThrowIfNull(version);

            string documentName = version.OpenApiDocumentName;
            if (!registeredDocumentNames.Add(documentName))
            {
                throw new ArgumentException(
                    $"Duplicate API version document name '{documentName}' is not allowed.",
                    nameof(versions));
            }

            string versionedPrefix = $"{prefix}/{version.RouteSegment}";
            services.AddOpenApi(documentName, options =>
            {
                options.AddDocumentTransformer<MultipartFormRequestBodyDocumentTransformer>();
                options.AddDocumentTransformer((document, _, _) =>
                {
                    document.Info.Version = version.Version.ToString();
                    return Task.CompletedTask;
                });
                options.ShouldInclude = description =>
                    description.RelativePath is string relativePath
                    && (string.Equals(relativePath, versionedPrefix, StringComparison.OrdinalIgnoreCase)
                        || relativePath.StartsWith($"{versionedPrefix}/", StringComparison.OrdinalIgnoreCase));
            });
        }
    }
}
