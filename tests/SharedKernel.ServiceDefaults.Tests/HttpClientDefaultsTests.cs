using System.Net;
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
        handlers.Where(HandlerChain.IsServiceDiscovery).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Service_discovery_runs_once_inside_the_resilience_handler_so_every_retry_resolves_again()
    {
        // Arrange
        await using var app = DefaultsApplication.Build("Production");
        var factory = app.Services.GetRequiredService<IHttpMessageHandlerFactory>();

        // Act
        var handlers = HandlerChain.Of(factory.CreateHandler("any-client"));

        // Assert
        var resilience = handlers.FindIndex(handler => handler is ResilienceHandler);
        var discovery = handlers.FindIndex(HandlerChain.IsServiceDiscovery);
        handlers.Where(HandlerChain.IsServiceDiscovery).ShouldHaveSingleItem();
        resilience.ShouldBeGreaterThanOrEqualTo(0);
        discovery.ShouldBeGreaterThan(resilience);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("CONNECT")]
    public async Task An_unsafe_request_is_not_retried(string method)
    {
        // Act
        var (status, attempts) = await TransientRequest.Send(new HttpMethod(method), failures: 1);

        // Assert
        status.ShouldBe(HttpStatusCode.ServiceUnavailable);
        attempts.ShouldBe(1);
    }

    [Fact]
    public async Task A_safe_request_is_retried()
    {
        // Act
        var (status, attempts) = await TransientRequest.Send(HttpMethod.Get, failures: 1);

        // Assert
        status.ShouldBe(HttpStatusCode.OK);
        attempts.ShouldBe(2);
    }

    [Fact]
    public async Task The_resilience_callback_changes_the_standard_options()
    {
        // Act
        var (status, attempts) = await TransientRequest.Send(HttpMethod.Get, failures: 2, options => options.Retry.MaxRetryAttempts = 1);

        // Assert
        status.ShouldBe(HttpStatusCode.ServiceUnavailable);
        attempts.ShouldBe(2);
    }

    [Fact]
    public async Task The_resilience_callback_cannot_turn_retries_of_unsafe_requests_back_on()
    {
        // Act
        var (status, attempts) = await TransientRequest.Send(HttpMethod.Post, failures: 1, options => options.Retry.ShouldHandle = _ => ValueTask.FromResult(true));

        // Assert
        status.ShouldBe(HttpStatusCode.ServiceUnavailable);
        attempts.ShouldBe(1);
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
