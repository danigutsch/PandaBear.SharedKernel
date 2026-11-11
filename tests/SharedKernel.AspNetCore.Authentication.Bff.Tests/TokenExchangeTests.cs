using System.Net;

namespace PandaBear.AspNetCore.Authentication.Bff.Tests;

/// <summary>Tests RFC 8693 token exchange: the request, caching per session and audience, and failures.</summary>
public sealed class TokenExchangeTests
{
    private const string TokenExchangeGrant = "urn:ietf:params:oauth:grant-type:token-exchange";

    [Fact]
    public async Task The_exchange_request_follows_rfc_8693()
    {
        // Arrange
        using var host = new BffTestHost();
        var user = await host.SignIn("alice");

        // Act
        var result = await host.Manager.GetAudienceAccessToken(user, "orders-api", "orders", TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.AccessToken.ShouldBe("exchanged-orders-api-1");
        result.ExpiresAt.ShouldBe(BffTestHost.Start.AddSeconds(300));
        var exchange = host.IdentityProvider.Grants(TokenExchangeGrant).ShouldHaveSingleItem();
        exchange.Uri.ShouldBe(FakeIdentityProvider.TokenEndpoint);
        exchange.Form["subject_token"].ShouldBe("access-0");
        exchange.Form["subject_token_type"].ShouldBe("urn:ietf:params:oauth:token-type:access_token");
        exchange.Form["requested_token_type"].ShouldBe("urn:ietf:params:oauth:token-type:access_token");
        exchange.Form["audience"].ShouldBe("orders-api");
        exchange.Form["scope"].ShouldBe("orders");
        exchange.Form["client_id"].ShouldBe("bff-client");
        exchange.Form["client_secret"].ShouldBe("bff-secret");
    }

    [Fact]
    public async Task An_exchange_without_a_scope_omits_the_scope_parameter()
    {
        // Arrange
        using var host = new BffTestHost();
        var user = await host.SignIn("alice");

        // Act
        await host.Manager.GetAudienceAccessToken(user, "orders-api", scope: null, TestContext.Current.CancellationToken);

        // Assert
        host.IdentityProvider.Grants(TokenExchangeGrant).ShouldHaveSingleItem().Form.ContainsKey("scope").ShouldBeFalse();
    }

    [Fact]
    public async Task Exchanged_tokens_are_cached_per_audience_until_they_near_expiry()
    {
        // Arrange
        using var host = new BffTestHost(options => options.RefreshBeforeExpiry = TimeSpan.FromSeconds(30));
        host.IdentityProvider.ExpiresIn = 600;
        var user = await host.SignIn("alice", expiresIn: TimeSpan.FromHours(1));
        var ct = TestContext.Current.CancellationToken;

        // Act
        var first = await host.Manager.GetAudienceAccessToken(user, "orders-api", null, ct);
        var cached = await host.Manager.GetAudienceAccessToken(user, "orders-api", null, ct);
        var otherAudience = await host.Manager.GetAudienceAccessToken(user, "billing-api", null, ct);
        host.Time.Advance(TimeSpan.FromSeconds(571));
        var renewed = await host.Manager.GetAudienceAccessToken(user, "orders-api", null, ct);

        // Assert
        cached.AccessToken.ShouldBe(first.AccessToken);
        otherAudience.AccessToken.ShouldBe("exchanged-billing-api-2");
        renewed.AccessToken.ShouldBe("exchanged-orders-api-3");
        host.IdentityProvider.Grants(TokenExchangeGrant).Count.ShouldBe(3);
    }

    [Fact]
    public async Task A_refreshed_source_token_invalidates_the_cached_exchange()
    {
        // Arrange
        using var host = new BffTestHost();
        host.IdentityProvider.ExpiresIn = 3600;
        var user = await host.SignIn("alice", expiresIn: TimeSpan.FromMinutes(5));
        var ct = TestContext.Current.CancellationToken;
        await host.Manager.GetAudienceAccessToken(user, "orders-api", null, ct);
        host.Time.Advance(TimeSpan.FromMinutes(10));

        // Act
        await host.Manager.GetAudienceAccessToken(user, "orders-api", null, ct);

        // Assert
        var exchanges = host.IdentityProvider.Grants(TokenExchangeGrant);
        exchanges.Count.ShouldBe(2);
        exchanges[1].Form["subject_token"].ShouldBe("access-2");
    }

    [Fact]
    public async Task Exchanged_tokens_are_isolated_per_session()
    {
        // Arrange
        using var host = new BffTestHost();
        var alice = await host.SignIn("alice", accessToken: "alice-access");
        var bob = await host.SignIn("bob", accessToken: "bob-access");
        var ct = TestContext.Current.CancellationToken;

        // Act
        var aliceToken = await host.Manager.GetAudienceAccessToken(alice, "orders-api", null, ct);
        var bobToken = await host.Manager.GetAudienceAccessToken(bob, "orders-api", null, ct);

        // Assert
        aliceToken.AccessToken.ShouldNotBe(bobToken.AccessToken);
        var exchanges = host.IdentityProvider.Grants(TokenExchangeGrant);
        exchanges.Select(exchange => exchange.Form["subject_token"]).ShouldBe(["alice-access", "bob-access"]);
    }

    [Fact]
    public async Task Concurrent_exchanges_for_one_audience_share_one_request()
    {
        // Arrange
        using var host = new BffTestHost();
        host.IdentityProvider.Delay = TimeSpan.FromMilliseconds(100);
        var user = await host.SignIn("alice");

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => Task.Run(() => host.Manager.GetAudienceAccessToken(user, "orders-api", null, TestContext.Current.CancellationToken))));

        // Assert
        host.IdentityProvider.Grants(TokenExchangeGrant).Count.ShouldBe(1);
        results.Select(result => result.AccessToken).Distinct().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("rejected")]
    [InlineData("same-token")]
    [InlineData("wrong-issued-type")]
    [InlineData("no-lifetime")]
    [InlineData("not-bearer")]
    public async Task An_unusable_exchange_answer_fails_typed_without_ending_the_session(string answer)
    {
        // Arrange
        using var host = new BffTestHost();
        host.IdentityProvider.Respond = request => answer switch
        {
            "rejected" => FakeIdentityProvider.Json(HttpStatusCode.BadRequest, new { error = "access_denied" }),
            "same-token" => FakeIdentityProvider.Json(HttpStatusCode.OK, new { access_token = request.Form["subject_token"], token_type = "Bearer", issued_token_type = "urn:ietf:params:oauth:token-type:access_token", expires_in = 60 }),
            "wrong-issued-type" => FakeIdentityProvider.Json(HttpStatusCode.OK, new { access_token = "x", token_type = "Bearer", issued_token_type = "urn:ietf:params:oauth:token-type:refresh_token", expires_in = 60 }),
            "no-lifetime" => FakeIdentityProvider.Json(HttpStatusCode.OK, new { access_token = "x", token_type = "Bearer", issued_token_type = "urn:ietf:params:oauth:token-type:access_token" }),
            _ => FakeIdentityProvider.Json(HttpStatusCode.OK, new { access_token = "x", token_type = "N_A", issued_token_type = "urn:ietf:params:oauth:token-type:access_token", expires_in = 60 })
        };
        var user = await host.SignIn("alice");

        // Act
        var result = await host.Manager.GetAudienceAccessToken(user, "orders-api", null, TestContext.Current.CancellationToken);

        // Assert
        result.Failure.ShouldBe(UserTokenFailure.ExchangeFailed);
        var userToken = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);
        userToken.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task An_exchanged_token_never_outlives_the_session_in_the_cache()
    {
        // Arrange
        using var host = new BffTestHost(options => options.SessionLifetime = TimeSpan.FromMinutes(2));
        host.IdentityProvider.ExpiresIn = 3600;
        var user = await host.SignIn("alice");

        // Act
        await host.Manager.GetAudienceAccessToken(user, "orders-api", null, TestContext.Current.CancellationToken);

        // Assert
        var exchangeKey = host.Cache.Entries.Keys.Where(key => key.StartsWith("bff:exchanged-token:", StringComparison.Ordinal)).ShouldHaveSingleItem();
        host.Cache.Options[exchangeKey].AbsoluteExpiration.ShouldBe(BffTestHost.Start.AddMinutes(2));
    }
}
