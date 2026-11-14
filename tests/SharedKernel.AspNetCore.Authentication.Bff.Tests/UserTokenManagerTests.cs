using System.Net;
using System.Security.Claims;
using System.Text;

namespace PandaBear.AspNetCore.Authentication.Bff.Tests;

/// <summary>Tests that user tokens are kept per session, refreshed once before expiry, rotated and never logged.</summary>
public sealed class UserTokenManagerTests
{
    [Fact]
    public async Task A_fresh_access_token_is_returned_without_calling_the_identity_provider()
    {
        // Arrange
        using var host = new BffTestHost();
        var user = await host.SignIn("alice");

        // Act
        var result = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.AccessToken.ShouldBe("access-0");
        host.IdentityProvider.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_token_inside_the_refresh_margin_is_refreshed_with_the_refresh_token_grant()
    {
        // Arrange
        using var host = new BffTestHost(options => options.RefreshBeforeExpiry = TimeSpan.FromSeconds(30));
        var user = await host.SignIn("alice", expiresIn: TimeSpan.FromMinutes(5));
        host.Time.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(31));

        // Act
        var result = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.AccessToken.ShouldBe("access-1");
        var refresh = host.IdentityProvider.Grants("refresh_token").ShouldHaveSingleItem();
        refresh.Uri.ShouldBe(FakeIdentityProvider.TokenEndpoint);
        refresh.Form["refresh_token"].ShouldBe("refresh-0");
        refresh.Form["client_id"].ShouldBe("bff-client");
        refresh.Form["client_secret"].ShouldBe("bff-secret");
    }

    [Fact]
    public async Task A_token_just_outside_the_refresh_margin_is_still_used()
    {
        // Arrange
        using var host = new BffTestHost(options => options.RefreshBeforeExpiry = TimeSpan.FromSeconds(30));
        var user = await host.SignIn("alice", expiresIn: TimeSpan.FromMinutes(5));
        host.Time.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(29));

        // Act
        var result = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        result.AccessToken.ShouldBe("access-0");
        host.IdentityProvider.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Concurrent_requests_for_an_expired_token_share_one_refresh()
    {
        // Arrange
        using var host = new BffTestHost();
        host.IdentityProvider.Delay = TimeSpan.FromMilliseconds(100);
        var user = await host.SignIn("alice");
        host.Time.Advance(TimeSpan.FromMinutes(10));

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken))));

        // Assert
        host.IdentityProvider.Grants("refresh_token").Count.ShouldBe(1);
        results.Select(result => result.AccessToken).Distinct().ShouldHaveSingleItem().ShouldBe("access-1");
    }

    [Fact]
    public async Task A_rotated_refresh_token_replaces_the_old_one()
    {
        // Arrange
        using var host = new BffTestHost();
        var user = await host.SignIn("alice");
        host.Time.Advance(TimeSpan.FromMinutes(10));
        await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);
        host.Time.Advance(TimeSpan.FromMinutes(10));

        // Act
        var result = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        result.AccessToken.ShouldBe("access-2");
        var grants = host.IdentityProvider.Grants("refresh_token");
        grants.Count.ShouldBe(2);
        grants[1].Form["refresh_token"].ShouldBe("refresh-1");
    }

    [Fact]
    public async Task A_refresh_answer_without_a_refresh_token_keeps_the_previous_refresh_token()
    {
        // Arrange
        using var host = new BffTestHost();
        host.IdentityProvider.RotateRefreshTokens = false;
        var user = await host.SignIn("alice");
        host.Time.Advance(TimeSpan.FromMinutes(10));
        await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);
        host.Time.Advance(TimeSpan.FromMinutes(10));

        // Act
        await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        host.IdentityProvider.Grants("refresh_token")[1].Form["refresh_token"].ShouldBe("refresh-0");
    }

    [Fact]
    public async Task A_refresh_answer_without_expires_in_uses_the_default_lifetime_and_keeps_the_rotated_refresh_token()
    {
        // Arrange
        using var host = new BffTestHost(options => options.DefaultAccessTokenLifetime = TimeSpan.FromMinutes(3));
        host.IdentityProvider.Respond = request => request.Form.GetValueOrDefault("grant_type") == "refresh_token"
            ? FakeIdentityProvider.Json(HttpStatusCode.OK, new Dictionary<string, object>
            {
                ["access_token"] = "access-new",
                ["token_type"] = "Bearer",
                ["refresh_token"] = "refresh-new"
            })
            : null;
        var user = await host.SignIn("alice");
        host.Time.Advance(TimeSpan.FromMinutes(10));

        // Act
        var result = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        result.AccessToken.ShouldBe("access-new");
        result.ExpiresAt.ShouldBe(host.Time.GetUtcNow().AddMinutes(3));
        var tokens = await host.Manager.GetTokens(user, TestContext.Current.CancellationToken);
        tokens!.RefreshToken.ShouldBe("refresh-new");
    }

    [Theory]
    [InlineData(null, 300)]
    [InlineData("access-new", 0)]
    public async Task An_unusable_refresh_answer_fails_but_keeps_the_rotated_refresh_token(string? accessToken, int expiresIn)
    {
        // Arrange
        using var host = new BffTestHost();
        host.IdentityProvider.Respond = request => request.Form.GetValueOrDefault("grant_type") == "refresh_token"
            ? FakeIdentityProvider.Json(HttpStatusCode.OK, new Dictionary<string, object?>
            {
                ["access_token"] = accessToken,
                ["token_type"] = "Bearer",
                ["expires_in"] = expiresIn,
                ["refresh_token"] = "refresh-new"
            })
            : null;
        var user = await host.SignIn("alice");
        host.Time.Advance(TimeSpan.FromMinutes(10));

        // Act
        var result = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        result.Failure.ShouldBe(UserTokenFailure.IdentityProviderUnavailable);
        var tokens = await host.Manager.GetTokens(user, TestContext.Current.CancellationToken);
        tokens!.RefreshToken.ShouldBe("refresh-new");
        tokens.AccessToken.ShouldBe("access-0");
    }

    [Fact]
    public async Task A_rejected_refresh_fails_typed_and_ends_the_token_session()
    {
        // Arrange
        using var host = new BffTestHost();
        host.IdentityProvider.Respond = _ => FakeIdentityProvider.Json(HttpStatusCode.BadRequest, new { error = "invalid_grant" });
        var user = await host.SignIn("alice");
        host.Time.Advance(TimeSpan.FromMinutes(10));

        // Act
        var failed = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);
        var afterwards = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        failed.Succeeded.ShouldBeFalse();
        failed.Failure.ShouldBe(UserTokenFailure.RefreshFailed);
        failed.AccessToken.ShouldBeNull();
        afterwards.Failure.ShouldBe(UserTokenFailure.NoTokens);
        var active = await host.Manager.HasActiveSession(user, TestContext.Current.CancellationToken);
        active.ShouldBeFalse();
    }

    [Fact]
    public async Task An_unavailable_identity_provider_fails_typed_and_keeps_the_tokens_for_a_retry()
    {
        // Arrange
        using var host = new BffTestHost();
        host.IdentityProvider.Respond = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var user = await host.SignIn("alice");
        host.Time.Advance(TimeSpan.FromMinutes(10));

        // Act
        var failed = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);
        host.IdentityProvider.Respond = null;
        var retried = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        failed.Failure.ShouldBe(UserTokenFailure.IdentityProviderUnavailable);
        retried.AccessToken.ShouldBe("access-1");
    }

    [Fact]
    public async Task A_slow_identity_provider_is_cut_off_by_the_timeout()
    {
        // Arrange
        using var host = new BffTestHost(options => options.IdentityProviderTimeout = TimeSpan.FromMilliseconds(100));
        host.IdentityProvider.Delay = TimeSpan.FromSeconds(30);
        var user = await host.SignIn("alice");
        host.Time.Advance(TimeSpan.FromMinutes(10));

        // Act
        var result = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        result.Failure.ShouldBe(UserTokenFailure.IdentityProviderUnavailable);
    }

    [Fact]
    public async Task An_expired_token_without_a_refresh_token_cannot_be_renewed()
    {
        // Arrange
        using var host = new BffTestHost();
        var user = await host.SignIn("alice", refreshToken: null);
        host.Time.Advance(TimeSpan.FromMinutes(10));

        // Act
        var result = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        result.Failure.ShouldBe(UserTokenFailure.RefreshFailed);
        host.IdentityProvider.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sessions_are_isolated_from_each_other()
    {
        // Arrange
        using var host = new BffTestHost();
        var alice = await host.SignIn("alice", accessToken: "alice-access");
        var bob = await host.SignIn("bob", accessToken: "bob-access");

        // Act
        var aliceToken = await host.Manager.GetAccessToken(alice, TestContext.Current.CancellationToken);
        var bobToken = await host.Manager.GetAccessToken(bob, TestContext.Current.CancellationToken);

        // Assert
        aliceToken.AccessToken.ShouldBe("alice-access");
        bobToken.AccessToken.ShouldBe("bob-access");
        alice.FindFirst(BffClaimTypes.SessionId)!.Value.ShouldNotBe(bob.FindFirst(BffClaimTypes.SessionId)!.Value);
    }

    [Fact]
    public async Task A_protected_entry_copied_to_another_session_cannot_be_read()
    {
        // Arrange
        using var host = new BffTestHost();
        var alice = await host.SignIn("alice", accessToken: "alice-access");
        var bob = await host.SignIn("bob", accessToken: "bob-access");
        var aliceKey = host.Cache.Entries.Keys.Where(key => key.EndsWith(alice.FindFirst(BffClaimTypes.SessionId)!.Value, StringComparison.Ordinal)).ShouldHaveSingleItem();
        var bobKey = host.Cache.Entries.Keys.Where(key => key.EndsWith(bob.FindFirst(BffClaimTypes.SessionId)!.Value, StringComparison.Ordinal)).ShouldHaveSingleItem();
        host.Cache.Entries[bobKey] = host.Cache.Entries[aliceKey];

        // Act
        var result = await host.Manager.GetAccessToken(bob, TestContext.Current.CancellationToken);

        // Assert
        result.Failure.ShouldBe(UserTokenFailure.NoTokens);
        host.Cache.Entries.ContainsKey(bobKey).ShouldBeFalse();
    }

    [Fact]
    public async Task Tokens_are_protected_at_rest_and_expire_with_the_session()
    {
        // Arrange
        using var host = new BffTestHost(options => options.SessionLifetime = TimeSpan.FromHours(2));

        // Act
        var user = await host.SignIn("alice", accessToken: "plain-access-token", refreshToken: "plain-refresh-token");

        // Assert
        var (key, value) = host.Cache.Entries.ShouldHaveSingleItem();
        key.ShouldStartWith("bff:user-tokens:", StringComparison.Ordinal);
        var text = Encoding.UTF8.GetString(value);
        text.ShouldNotContain("plain-access-token", StringComparison.Ordinal);
        text.ShouldNotContain("plain-refresh-token", StringComparison.Ordinal);
        host.Cache.Options[key].AbsoluteExpiration.ShouldBe(BffTestHost.Start.AddHours(2));
        user.FindFirst(BffClaimTypes.SessionExpiresAt)!.Value.ShouldBe("2026-11-11T14:00:00.0000000+00:00");
    }

    [Fact]
    public async Task A_custom_cache_key_prefix_is_used()
    {
        // Arrange
        using var host = new BffTestHost(options => options.CacheKeyPrefix = "app-a:");

        // Act
        await host.SignIn("alice");

        // Assert
        host.Cache.Entries.Keys.ShouldHaveSingleItem().ShouldStartWith("app-a:user-tokens:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_expired_session_is_refused_even_with_a_fresh_token()
    {
        // Arrange
        using var host = new BffTestHost(options => options.SessionLifetime = TimeSpan.FromMinutes(1));
        var user = await host.SignIn("alice", expiresIn: TimeSpan.FromHours(1));
        host.Time.Advance(TimeSpan.FromMinutes(2));

        // Act
        var result = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        result.Failure.ShouldBe(UserTokenFailure.SessionExpired);
    }

    [Fact]
    public async Task A_user_without_a_session_gets_a_typed_failure()
    {
        // Arrange
        using var host = new BffTestHost();
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = await host.Manager.GetAccessToken(anonymous, TestContext.Current.CancellationToken);

        // Assert
        result.Failure.ShouldBe(UserTokenFailure.NoSession);
    }

    [Fact]
    public async Task Tokens_never_reach_logs_or_result_text_even_on_failure()
    {
        // Arrange
        using var host = new BffTestHost();
        host.IdentityProvider.Respond = request => request.Form.GetValueOrDefault("grant_type") == "refresh_token"
            ? FakeIdentityProvider.Json(HttpStatusCode.BadRequest, new
            {
                error = "invalid_grant",
                error_description = "secret-refresh-token is invalid"
            })
            : null;
        var user = await host.SignIn("alice", accessToken: "secret-access-token", refreshToken: "secret-refresh-token");
        var fresh = await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);
        host.Time.Advance(TimeSpan.FromMinutes(10));

        // Act
        await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        host.Logs.Records.ShouldNotBeEmpty();
        var logs = host.AllLogText();
        logs.ShouldNotContain("secret-access-token", StringComparison.Ordinal);
        logs.ShouldNotContain("secret-refresh-token", StringComparison.Ordinal);
        fresh.ToString().ShouldNotContain("secret-access-token", StringComparison.Ordinal);
        new UserTokenSet("secret-access-token", BffTestHost.Start, "secret-refresh-token", "id", null, null)
            .ToString().ShouldNotContain("secret", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_identity_token_type_and_scope_survive_a_round_trip_through_the_protected_store()
    {
        // Arrange
        using var host = new BffTestHost();
        var user = await host.SignIn("alice");

        // Act
        var tokens = await host.Manager.GetTokens(user, TestContext.Current.CancellationToken);

        // Assert
        tokens.ShouldNotBeNull();
        tokens.AccessToken.ShouldBe("access-0");
        tokens.RefreshToken.ShouldBe("refresh-0");
        tokens.IdentityToken.ShouldBe("id-0");
        tokens.AccessTokenType.ShouldBe("Bearer");
        tokens.Scope.ShouldBe("openid profile");
        tokens.AccessTokenExpiresAt.ShouldBe(BffTestHost.Start.AddMinutes(5));
    }

    [Fact]
    public async Task Basic_client_authentication_sends_form_encoded_credentials_in_the_header()
    {
        // Arrange
        using var host = new BffTestHost(options => options.ClientAuthentication = TokenEndpointClientAuthentication.BasicHeader);
        var user = await host.SignIn("alice");
        host.Time.Advance(TimeSpan.FromMinutes(10));

        // Act
        await host.Manager.GetAccessToken(user, TestContext.Current.CancellationToken);

        // Assert
        var refresh = host.IdentityProvider.Grants("refresh_token").ShouldHaveSingleItem();
        refresh.Form.ContainsKey("client_secret").ShouldBeFalse();
        refresh.Authorization.ShouldBe("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("bff-client:bff-secret")));
    }
}
