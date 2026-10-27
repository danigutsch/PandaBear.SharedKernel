using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace PandaBear.ApiVersioning.AspNetCore.Tests;

/// <summary>Helpers for inspecting the endpoints registered on a test web application.</summary>
internal static class ApiVersionEndpointTestHost
{
    /// <summary>Returns the only <see cref="RouteEndpoint" /> registered on the application, failing if there is not exactly one.</summary>
    /// <param name="app">The application whose endpoint data sources are searched.</param>
    /// <returns>The single registered route endpoint.</returns>
    public static RouteEndpoint GetSingleEndpoint(WebApplication app)
    {
        var routeBuilder = (IEndpointRouteBuilder)app;
        return routeBuilder.DataSources.SelectMany(static dataSource => dataSource.Endpoints).OfType<RouteEndpoint>().Single();
    }
}
