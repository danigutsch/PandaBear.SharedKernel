using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace SharedKernel.ApiVersioning.AspNetCore.Tests;

internal static class ApiVersionEndpointTestHost
{
    public static RouteEndpoint GetSingleEndpoint(WebApplication app)
    {
        var routeBuilder = (IEndpointRouteBuilder)app;
        return routeBuilder.DataSources.SelectMany(static dataSource => dataSource.Endpoints).OfType<RouteEndpoint>().Single();
    }
}
