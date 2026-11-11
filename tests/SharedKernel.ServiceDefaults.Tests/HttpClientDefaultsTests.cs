using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.ServiceDiscovery;

namespace PandaBear.ServiceDefaults.Tests;

/// <summary>Tests the HttpClient part of the service defaults: service discovery and the standard resilience handler.</summary>
public sealed class HttpClientDefaultsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Every_http_client_gets_the_standard_resilience_handler_unless_it_is_turned_off(bool addResilience)
    {
        // Arrange
        await using var app = DefaultsApplication.Build("Production", options => options.AddResilience = addResilience);
        var factory = app.Services.GetRequiredService<IHttpMessageHandlerFactory>();

        // Act
        var handlers = HandlerChain.Of(factory.CreateHandler("any-client"));

        // Assert
        handlers.OfType<ResilienceHandler>().Any().ShouldBe(addResilience);
    }

    [Fact]
    public async Task Service_discovery_is_registered()
    {
        // Arrange
        await using var app = DefaultsApplication.Build("Production");

        // Act
        var resolver = app.Services.GetService<ServiceEndpointResolver>();

        // Assert
        resolver.ShouldNotBeNull();
    }
}
