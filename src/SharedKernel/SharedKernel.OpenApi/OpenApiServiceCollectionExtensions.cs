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

        services.AddBoundaryOpenApiDocuments(
            [.. boundaryNames.Select(boundaryName => new OpenApiBoundaryDocument(boundaryName, boundaryName))]);
    }

    /// <summary>
    /// Registers the default OpenAPI document and one named document per boundary definition.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="boundaries">The boundary documents to register.</param>
    public static void AddBoundaryOpenApiDocuments(
        this IServiceCollection services,
        IReadOnlyCollection<OpenApiBoundaryDocument> boundaries)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(boundaries);

        services.AddOpenApi();
        var registeredDocumentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var boundary in boundaries)
        {
            ArgumentNullException.ThrowIfNull(boundary);
            ArgumentException.ThrowIfNullOrWhiteSpace(boundary.DocumentName);
            ArgumentException.ThrowIfNullOrWhiteSpace(boundary.RoutePrefix);

            if (!registeredDocumentNames.Add(boundary.DocumentName))
            {
                throw new ArgumentException(
                    $"Duplicate OpenAPI document name '{boundary.DocumentName}' is not allowed.",
                    nameof(boundaries));
            }

            var routePrefix = boundary.RoutePrefix.Trim().Trim('/');
            if (routePrefix.Length == 0)
            {
                throw new ArgumentException("Boundary route prefixes must contain at least one non-slash character.", nameof(boundaries));
            }

            services.AddOpenApi(boundary.DocumentName, options =>
            {
                options.AddDocumentTransformer<MultipartFormRequestBodyDocumentTransformer>();
                options.ShouldInclude = description =>
                    description.RelativePath is string relativePath
                    && (string.Equals(relativePath, routePrefix, StringComparison.OrdinalIgnoreCase)
                        || relativePath.StartsWith($"{routePrefix}/", StringComparison.OrdinalIgnoreCase));
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
        var prefix = routePrefix.Trim().Trim('/');
        if (prefix.Length == 0)
        {
            throw new ArgumentException("The route prefix must contain at least one non-slash character.", nameof(routePrefix));
        }

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
                options.AddDocumentTransformer((document, context, ct) =>
                {
                    _ = context;
                    _ = ct;
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
