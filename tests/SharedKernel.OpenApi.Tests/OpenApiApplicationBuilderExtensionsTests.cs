using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace SharedKernel.OpenApi.Tests;

/// <summary>
/// Verifies runtime OpenAPI endpoint mapping behavior.
/// </summary>
[Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.EndpointCategory)]
public sealed class OpenApiApplicationBuilderExtensionsTests
{
    [Fact]
    public void Maps_openapi_endpoints_for_development()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Services.AddOpenApi();
        using var application = builder.Build();

        // Act
        application.MapConfiguredOpenApi();
        var endpoints = ((IEndpointRouteBuilder)application).DataSources.SelectMany(static source => source.Endpoints);

        // Assert
        endpoints.ShouldNotBeEmpty();
    }

    [Fact]
    public void Does_not_map_openapi_endpoints_for_a_production_build_generation_process()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["OpenApi:BuildGeneration"] = bool.TrueString });
        builder.Services.AddOpenApi();
        using var application = builder.Build();

        // Act
        application.MapConfiguredOpenApi();

        // Assert
        ((IEndpointRouteBuilder)application).DataSources.ShouldBeEmpty();
    }
}
