using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Observability;
using SharedKernel.ServiceDefaults;

namespace ServiceDefaultsConsumer;

/// <summary>
/// Tests that a host which keeps the official Aspire service-defaults template can call the SharedKernel registration methods too. The
/// namespace lies outside <c>SharedKernel</c>, as a consuming host's does, so the template's methods and the SharedKernel ones come from using
/// directives in the same scope and a name they shared would make the calls ambiguous or bind them to the template.
/// </summary>
public sealed class AspireTemplateCoexistenceTests
{
    [Fact]
    public async Task The_registration_methods_resolve_beside_the_template_methods()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();

        // Act
        builder.AddServiceDefaults().ConfigureOpenTelemetry().AddDefaultHealthChecks();
        builder.AddApplicationDefaults().AddObservability();
        await using var app = builder.Build();
        app.MapDefaultEndpoints().MapHealthEndpoints();

        // Assert
        var templateCalls = ((IHostApplicationBuilder)builder).Properties.Keys;
        templateCalls.ShouldContain("AddServiceDefaults");
        templateCalls.ShouldContain("ConfigureOpenTelemetry");
        templateCalls.ShouldContain("AddDefaultHealthChecks");
        ((IApplicationBuilder)app).Properties.Keys.ShouldContain("MapDefaultEndpoints");
        app.Services.GetService<ServiceDefaultsOptions>().ShouldNotBeNull();
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText)
            .Order(StringComparer.Ordinal)
            .ToArray()
            .ShouldBe(["/alive", "/health"]);
    }
}
