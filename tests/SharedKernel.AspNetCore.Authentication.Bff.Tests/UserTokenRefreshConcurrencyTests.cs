using System.Net;

namespace SharedKernel.AspNetCore.Authentication.Bff.Tests;

/// <summary>Tests how an in-flight refresh handles a changed or removed persisted token entry.</summary>
public sealed class UserTokenRefreshConcurrencyTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_in_flight_refresh_cannot_restore_a_removed_session(bool usableAccessToken)
    {
        // Arrange
        using var host = new BffTestHost();
        var ct = TestContext.Current.CancellationToken;
        var user = await host.SignIn("alice", expiresIn: TimeSpan.FromSeconds(20));
        var cacheKey = host.Cache.Entries.Keys.ShouldHaveSingleItem();
        host.IdentityProvider.Respond = _ =>
        {
            host.Cache.Remove(cacheKey);
            return FakeIdentityProvider.Json(HttpStatusCode.OK, new Dictionary<string, object?>
            {
                ["access_token"] = usableAccessToken ? "synthetic-new-access" : null,
                ["refresh_token"] = "synthetic-new-refresh",
                ["token_type"] = "Bearer",
                ["expires_in"] = 300
            });
        };

        // Act
        var result = await host.Manager.GetAccessToken(user, ct);

        // Assert
        result.Failure.ShouldBe(UserTokenFailure.NoTokens);
        host.Cache.Entries.ShouldBeEmpty();
        host.IdentityProvider.Grants("refresh_token").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_stale_rejection_cannot_remove_a_newer_token_entry()
    {
        // Arrange
        using var host = new BffTestHost();
        var ct = TestContext.Current.CancellationToken;
        var user = await host.SignIn("alice", expiresIn: TimeSpan.FromSeconds(20));
        var cacheKey = host.Cache.Entries.Keys.ShouldHaveSingleItem();
        var replacement = new UserTokenSet(
            "synthetic-current-access", host.Time.GetUtcNow().AddMinutes(10), "synthetic-current-refresh", null, "Bearer", null);
        var protectedReplacement = await host.ProtectReplacementTokens(user, replacement, ct);
        host.IdentityProvider.Respond = _ =>
        {
            host.Cache.Set(cacheKey, protectedReplacement, host.Cache.Options[cacheKey]);
            return FakeIdentityProvider.Json(HttpStatusCode.BadRequest, new { error = "invalid_grant" });
        };

        // Act
        var result = await host.Manager.GetAccessToken(user, ct);
        var stored = await host.Manager.GetTokens(user, ct);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.AccessToken.ShouldBe(replacement.AccessToken);
        stored.ShouldNotBeNull().RefreshToken.ShouldBe(replacement.RefreshToken);
        host.IdentityProvider.Grants("refresh_token").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_refresh_failure_cannot_return_the_access_token_of_a_removed_session()
    {
        // Arrange
        using var host = new BffTestHost();
        var ct = TestContext.Current.CancellationToken;
        var user = await host.SignIn("alice", expiresIn: TimeSpan.FromSeconds(20));
        var cacheKey = host.Cache.Entries.Keys.ShouldHaveSingleItem();
        host.IdentityProvider.Respond = _ =>
        {
            host.Cache.Remove(cacheKey);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        };

        // Act
        var result = await host.Manager.GetAccessToken(user, ct);

        // Assert
        result.Failure.ShouldBe(UserTokenFailure.NoTokens);
        host.Cache.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_registered_adapter_updates_the_protected_entry_and_preserves_session_expiry()
    {
        // Arrange
        using var host = new BffTestHost(useConditionalWrites: true);
        var ct = TestContext.Current.CancellationToken;
        var user = await host.SignIn("alice", expiresIn: TimeSpan.FromSeconds(20));
        var cacheKey = host.Cache.Entries.Keys.ShouldHaveSingleItem();
        var originalVersion = host.Cache.Entries[cacheKey];
        var expiresAt = host.Cache.Options[cacheKey].AbsoluteExpiration;

        // Act
        var result = await host.Manager.GetAccessToken(user, ct);
        var stored = await host.Manager.GetTokens(user, ct);

        // Assert
        result.Succeeded.ShouldBeTrue();
        stored.ShouldNotBeNull().RefreshToken.ShouldBe("refresh-1");
        var protectedVersionChanged = !originalVersion.AsSpan().SequenceEqual(host.Cache.Entries[cacheKey]);
        protectedVersionChanged.ShouldBeTrue();
        host.ConditionalWrites.ReplacementExpiration.ShouldBe(expiresAt);
        host.Cache.Options[cacheKey].AbsoluteExpiration.ShouldBe(expiresAt);
        host.IdentityProvider.Grants("refresh_token").ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_registered_adapter_preserves_the_newer_entry_at_the_mutation_boundary(bool grantRejected)
    {
        // Arrange
        using var host = new BffTestHost(useConditionalWrites: true);
        var ct = TestContext.Current.CancellationToken;
        var user = await host.SignIn("alice", expiresIn: TimeSpan.FromSeconds(20));
        var cacheKey = host.Cache.Entries.Keys.ShouldHaveSingleItem();
        var expiry = host.Cache.Options[cacheKey].AbsoluteExpiration;
        var replacement = new UserTokenSet(
            "synthetic-current-access", host.Time.GetUtcNow().AddMinutes(10), "synthetic-current-refresh", null, "Bearer", null);
        var protectedReplacement = await host.ProtectReplacementTokens(user, replacement, ct);
        host.ConditionalWrites.BeforeCompare = key => host.Cache.Set(key, protectedReplacement, host.Cache.Options[key]);
        if (grantRejected)
        {
            host.IdentityProvider.Respond = _ =>
                FakeIdentityProvider.Json(HttpStatusCode.BadRequest, new { error = "invalid_grant" });
        }

        // Act
        var result = await host.Manager.GetAccessToken(user, ct);
        var stored = await host.Manager.GetTokens(user, ct);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.AccessToken.ShouldBe(replacement.AccessToken);
        stored.ShouldNotBeNull().RefreshToken.ShouldBe(replacement.RefreshToken);
        host.ConditionalWrites.RemovalAttempts.ShouldBe(grantRejected ? 1 : 0);
        host.ConditionalWrites.ReplacementExpiration.ShouldBe(grantRejected ? null : expiry);
        host.IdentityProvider.Grants("refresh_token").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task The_registered_adapter_preserves_session_removal_at_the_write_boundary()
    {
        // Arrange
        using var host = new BffTestHost(useConditionalWrites: true);
        var ct = TestContext.Current.CancellationToken;
        var user = await host.SignIn("alice", expiresIn: TimeSpan.FromSeconds(20));
        host.ConditionalWrites.BeforeCompare = host.Cache.Remove;

        // Act
        var result = await host.Manager.GetAccessToken(user, ct);

        // Assert
        result.Failure.ShouldBe(UserTokenFailure.NoTokens);
        host.Cache.Entries.ShouldBeEmpty();
        host.ConditionalWrites.ReplacementExpiration.ShouldNotBeNull();
    }

    [Fact]
    public async Task The_registered_adapter_removes_the_current_entry_after_a_rejected_refresh()
    {
        // Arrange
        using var host = new BffTestHost(useConditionalWrites: true);
        var ct = TestContext.Current.CancellationToken;
        var user = await host.SignIn("alice", expiresIn: TimeSpan.FromSeconds(20));
        host.IdentityProvider.Respond = _ =>
            FakeIdentityProvider.Json(HttpStatusCode.BadRequest, new { error = "invalid_grant" });

        // Act
        var result = await host.Manager.GetAccessToken(user, ct);

        // Assert
        result.Failure.ShouldBe(UserTokenFailure.RefreshFailed);
        host.ConditionalWrites.RemovalAttempts.ShouldBe(1);
        host.Cache.Entries.ShouldBeEmpty();
    }
}
