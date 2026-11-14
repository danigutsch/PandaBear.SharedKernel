using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.AspNetCore.Authentication.Bff.Tests;

/// <summary>Tests that sign-out revokes the refresh token and removes every stored token of the session.</summary>
public sealed class SignOutTests
{
    [Fact]
    public async Task Sign_out_revokes_the_refresh_token_and_removes_user_and_exchanged_tokens()
    {
        // Arrange
        using var host = new BffTestHost(services: services => services.AddHttpClient("orders").AddTokenExchangeHandler("orders-api"));
        var user = await host.SignIn("alice");
        var other = await host.SignIn("bob");
        await host.Manager.GetAudienceAccessToken(user, "orders-api", null, TestContext.Current.CancellationToken);
        var sessionId = user.FindFirst(BffClaimTypes.SessionId)!.Value;

        // Act
        await host.Manager.SignOut(user, TestContext.Current.CancellationToken);

        // Assert
        var revocation = host.IdentityProvider.Requests.Where(request => request.Uri == FakeIdentityProvider.RevocationEndpoint).ShouldHaveSingleItem();
        revocation.Form["token"].ShouldBe("refresh-0");
        revocation.Form["token_type_hint"].ShouldBe("refresh_token");
        revocation.Form["client_id"].ShouldBe("bff-client");
        host.Cache.Entries.Keys.ShouldNotContain(key => key.Contains(sessionId, StringComparison.Ordinal));
        var signedOut = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);
        signedOut.Failure.ShouldBe(UserTokenFailure.NoTokens);
        var untouched = await host.Manager.GetAccessToken(other, TestContext.Current.CancellationToken);
        untouched.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Sign_out_without_a_revocation_endpoint_only_removes_the_tokens()
    {
        // Arrange
        using var host = new BffTestHost(revocationEndpoint: false);
        var user = await host.SignIn("alice");

        // Act
        await host.Manager.SignOut(user, TestContext.Current.CancellationToken);

        // Assert
        host.IdentityProvider.Requests.ShouldBeEmpty();
        host.Cache.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Revocation_can_be_turned_off()
    {
        // Arrange
        using var host = new BffTestHost(options => options.RevokeRefreshTokenOnSignOut = false);
        var user = await host.SignIn("alice");

        // Act
        await host.Manager.SignOut(user, TestContext.Current.CancellationToken);

        // Assert
        host.IdentityProvider.Requests.ShouldBeEmpty();
        host.Cache.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_revocation_still_removes_the_tokens_and_logs_no_token()
    {
        // Arrange
        using var host = new BffTestHost();
        host.IdentityProvider.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var user = await host.SignIn("alice", refreshToken: "secret-refresh-token");

        // Act
        await host.Manager.SignOut(user, TestContext.Current.CancellationToken);

        // Assert
        host.Cache.Entries.ShouldBeEmpty();
        host.Logs.Records.ShouldNotBeEmpty();
        host.AllLogText().ShouldNotContain("secret-refresh-token", StringComparison.Ordinal);
    }
}
