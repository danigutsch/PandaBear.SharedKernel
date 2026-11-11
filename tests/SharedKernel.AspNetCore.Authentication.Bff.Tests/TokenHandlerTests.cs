using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.AspNetCore.Authentication.Bff.Tests;

/// <summary>Tests the HttpClient handlers that attach the user's token or an exchanged token.</summary>
public sealed class TokenHandlerTests
{
    [Fact]
    public async Task The_user_access_token_handler_attaches_the_current_users_token()
    {
        // Arrange
        string? authorization = null;
        using var host = BffTestHost.WithApiClient(builder => builder.AddUserAccessTokenHandler(), request => authorization = request.Headers.Authorization?.ToString());
        var accessor = host.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = await host.SignIn("alice", accessToken: "alice-access") };
        var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("api");

        // Act
        using var response = await client.GetAsync(new Uri("https://api.test/orders"), TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        authorization.ShouldBe("Bearer alice-access");
    }

    [Fact]
    public async Task The_token_exchange_handler_attaches_the_exchanged_token_for_its_audience()
    {
        // Arrange
        string? authorization = null;
        using var host = BffTestHost.WithApiClient(builder => builder.AddTokenExchangeHandler("orders-api", "orders"), request => authorization = request.Headers.Authorization?.ToString());
        var accessor = host.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = await host.SignIn("alice") };
        var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("api");

        // Act
        using var response = await client.GetAsync(new Uri("https://api.test/orders"), TestContext.Current.CancellationToken);

        // Assert
        authorization.ShouldBe("Bearer exchanged-orders-api-1");
        host.IdentityProvider.Grants("urn:ietf:params:oauth:grant-type:token-exchange").ShouldHaveSingleItem().Form["scope"].ShouldBe("orders");
    }

    [Fact]
    public async Task A_missing_token_throws_a_typed_exception_before_anything_is_sent()
    {
        // Arrange
        var sent = false;
        using var host = BffTestHost.WithApiClient(builder => builder.AddUserAccessTokenHandler(), _ => sent = true);
        var accessor = host.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };
        var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("api");

        // Act
        Func<Task> send = () => client.GetAsync(new Uri("https://api.test/orders"), TestContext.Current.CancellationToken);

        // Assert
        var exception = await send.ShouldThrow<UserTokenUnavailableException>();
        exception.Failure.ShouldBe(UserTokenFailure.NoSession);
        sent.ShouldBeFalse();
    }

    [Fact]
    public void An_audience_must_not_be_blank()
    {
        // Arrange
        var builder = new ServiceCollection().AddHttpClient("api");

        // Act
        Action add = () => builder.AddTokenExchangeHandler(" ");

        // Assert
        add.ShouldThrow<ArgumentException>();
    }
}
