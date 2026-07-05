using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace SharedKernel.ApiVersioning.AspNetCore;

/// <summary>
/// Adds ASP.NET Core route helpers for API contract versions.
/// </summary>
public static class ApiVersionRouteGroupExtensions
{
    /// <summary>
    /// Maps a route group for a versioned API prefix such as <c>/api/v1</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="version">The API version definition.</param>
    /// <param name="routePrefix">The unversioned API route prefix.</param>
    /// <returns>The configured route group builder.</returns>
    public static RouteGroupBuilder MapApiVersionGroup(
        this IEndpointRouteBuilder endpoints,
        ApiVersionDefinition version,
        string routePrefix = "api")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(routePrefix);

        var prefix = routePrefix.Trim('/');
        return endpoints.MapGroup($"/{prefix}/{version.RouteSegment}")
            .WithMetadata(version);
    }

    /// <summary>
    /// Adds API version metadata to an endpoint or route group.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="version">The API version definition.</param>
    /// <returns>The configured endpoint convention builder.</returns>
    public static TBuilder WithApiVersion<TBuilder>(this TBuilder builder, ApiVersionDefinition version)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(version);

        builder.WithMetadata(version);
        return builder;
    }
}
