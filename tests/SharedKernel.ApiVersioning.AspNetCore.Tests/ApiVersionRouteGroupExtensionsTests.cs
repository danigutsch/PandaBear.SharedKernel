using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

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
    public void Maps_api_version_route_group_with_normalized_prefix()
    {
        // Arrange
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();
        var version = new ApiVersionDefinition(new ApiVersion(1, 1));

        // Act
        app.MapApiVersionGroup(version, " /admin-api/ ").MapGet("/status", () => "ok");

        // Assert
        var endpoint = ApiVersionEndpointTestHost.GetSingleEndpoint(app);
        endpoint.RoutePattern.RawText.ShouldBe("/admin-api/v1.1/status");
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

    [Fact]
    public void With_api_version_returns_original_builder()
    {
        // Arrange
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();
        var version = new ApiVersionDefinition(new ApiVersion(2));
        var routeBuilder = app.MapGet("/health", () => "ok");

        // Act
        var returnedBuilder = routeBuilder.WithApiVersion(version);

        // Assert
        returnedBuilder.ShouldBeSameAs(routeBuilder);
    }

    [Theory]
    [InlineData("/")]
    [InlineData(" /// ")]
    public void Rejects_empty_normalized_route_prefix(string routePrefix)
    {
        // Arrange
        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();
        var version = new ApiVersionDefinition(new ApiVersion(1));

        // Act
        Action action = () => app.MapApiVersionGroup(version, routePrefix);

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Rejects_null_endpoint_route_builder()
    {
        // Arrange
        IEndpointRouteBuilder? endpoints = null;
        var version = new ApiVersionDefinition(new ApiVersion(1));

        // Act
        Action action = () => endpoints!.MapApiVersionGroup(version);

        // Assert
        action.ShouldThrow<ArgumentNullException>();
    }
}
