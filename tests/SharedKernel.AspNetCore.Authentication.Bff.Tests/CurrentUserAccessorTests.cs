using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.AspNetCore.Authentication.Bff.Tests;

/// <summary>Tests where the token handlers find the current user: the HTTP request or the Blazor Server circuit.</summary>
public sealed class CurrentUserAccessorTests
{
    [Fact]
    public async Task Without_blazor_the_user_comes_from_the_HTTP_context()
    {
        // Arrange
        using var provider = CurrentUserTestServices.Build(blazor: false);
        var user = CurrentUserTestServices.Principal("alice");
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = user };

        // Act
        var current = await provider.GetRequiredService<ICurrentUserAccessor>().GetCurrentUser(TestContext.Current.CancellationToken);

        // Assert
        current.ShouldBeSameAs(user);
    }

    [Fact]
    public async Task Without_a_request_or_circuit_the_user_is_anonymous()
    {
        // Arrange
        using var provider = CurrentUserTestServices.Build(blazor: true);

        // Act
        var current = await provider.GetRequiredService<ICurrentUserAccessor>().GetCurrentUser(TestContext.Current.CancellationToken);

        // Assert
        current.Identity?.IsAuthenticated.ShouldNotBe(true);
    }

    [Fact]
    public async Task Inside_circuit_activity_the_user_comes_from_the_circuits_authentication_state()
    {
        // Arrange
        using var provider = CurrentUserTestServices.Build(blazor: true);
        using var circuitScope = provider.CreateScope();
        var circuitUser = CurrentUserTestServices.Principal("circuit-user");
        ((FixedAuthenticationStateProvider)circuitScope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>()).User = circuitUser;
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = CurrentUserTestServices.Principal("request-user") };
        var handler = circuitScope.ServiceProvider.GetServices<CircuitHandler>().ShouldHaveSingleItem();
        ClaimsPrincipal? seenInside = null;
        var activity = handler.CreateInboundActivityHandler(async _ =>
        {
            // A handler created by IHttpClientFactory lives in another scope; it still sees the circuit's services.
            using var handlerScope = provider.CreateScope();
            seenInside = await handlerScope.ServiceProvider.GetRequiredService<ICurrentUserAccessor>().GetCurrentUser(CancellationToken.None);
        });

        // Act
        await activity(null!);
        var seenAfter = await provider.GetRequiredService<ICurrentUserAccessor>().GetCurrentUser(TestContext.Current.CancellationToken);

        // Assert
        seenInside.ShouldBeSameAs(circuitUser);
        seenAfter.FindFirst("sub")!.Value.ShouldBe("request-user");
    }
}
