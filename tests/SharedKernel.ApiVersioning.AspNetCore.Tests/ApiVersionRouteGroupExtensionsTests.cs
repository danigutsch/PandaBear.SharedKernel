using Microsoft.AspNetCore.Builder;
namespace SharedKernel.ApiVersioning.AspNetCore.Tests;

/// <summary>
/// Verifies ASP.NET Core API version route helpers.
/// </summary>
public sealed class ApiVersionRouteGroupExtensionsTests
{
    [Fact]
    public void Maps_api_version_route_group_with_metadata()
    {
        // Arrange
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();
        var version = new ApiVersionDefinition(new ApiVersion(1));

        // Act
        app.MapApiVersionGroup(version).MapGet("/status", () => "ok");

        // Assert
        var endpoint = ApiVersionEndpointTestHost.GetSingleEndpoint(app);
        endpoint.RoutePattern.RawText.ShouldBe("/api/v1/status");
        endpoint.Metadata.GetMetadata<ApiVersionDefinition>().ShouldBe(version);
    }

    [Fact]
    public void Adds_api_version_metadata_to_endpoint()
    {
        // Arrange
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();
        var version = new ApiVersionDefinition(new ApiVersion(2, 1));

        // Act
        app.MapGet("/health", () => "ok").WithApiVersion(version);

        // Assert
        var endpoint = ApiVersionEndpointTestHost.GetSingleEndpoint(app);
        endpoint.Metadata.GetMetadata<ApiVersionDefinition>().ShouldBe(version);
    }
}
