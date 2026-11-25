namespace SharedKernel.AspNetCore.Authentication.Bff.Tests;

/// <summary>Verifies compatibility with the original persisted protection purposes.</summary>
public sealed class PersistedBffPurposeTests
{
    [Fact]
    public async Task Ticket_store_reads_and_writes_tickets_under_the_original_purpose()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var context = new TicketStoreTestContext();
        var store = context.CreateStore();
        var ticket = context.CreateTicket("fixture-subject", TimeSpan.FromMinutes(10));

        // Act
        var key = await store.StoreAsync(ticket, ct);
        var originalProtector = context.DataProtection.CreateProtector("PandaBear.AspNetCore.Authentication.Bff.ServerSideTicketStore.v1")
            .CreateProtector(key);
        var plaintext = originalProtector.Unprotect(context.Cache.Entries[key]);
        await context.Cache.SetAsync(key, originalProtector.Protect(plaintext), context.Cache.Options[key], ct);
        var restored = await store.RetrieveAsync(key, ct);

        // Assert
        restored.ShouldNotBeNull();
        var subject = restored.Principal.FindFirst("sub");
        subject.ShouldNotBeNull();
        subject.Value.ShouldBe("fixture-subject");
    }

    [Fact]
    public async Task Token_cache_reads_and_writes_user_tokens_under_the_original_purpose()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var context = new TicketStoreTestContext();
        var cache = new ProtectedTokenCache(context.Cache, context.DataProtection, "fixture-tokens:");
        var session = new UserSession("fixture-session", context.Time.GetUtcNow().AddMinutes(10));
        var tokens = new UserTokenSet("fixture-user-token", session.ExpiresAt, null, null, null, null);

        // Act
        await cache.SetUserTokens(session, tokens, ct);
        var entry = context.Cache.Entries.ShouldHaveSingleItem();
        var originalProtector = context.DataProtection.CreateProtector("PandaBear.AspNetCore.Authentication.Bff.UserTokens.v1")
            .CreateProtector(entry.Key);
        var plaintext = originalProtector.Unprotect(entry.Value);
        await context.Cache.SetAsync(entry.Key, originalProtector.Protect(plaintext), context.Cache.Options[entry.Key], ct);
        var restored = await cache.GetUserTokens(session, ct);

        // Assert
        restored.ShouldNotBeNull();
        restored.AccessToken.ShouldBe(tokens.AccessToken);
    }

    [Fact]
    public async Task Token_cache_reads_and_writes_exchanged_tokens_under_the_original_purpose()
    {
        // Arrange
        const string source = "fixture-source-token";
        const string exchanged = "fixture-exchanged-token";
        var ct = TestContext.Current.CancellationToken;
        var context = new TicketStoreTestContext();
        var cache = new ProtectedTokenCache(context.Cache, context.DataProtection, "fixture-tokens:");
        var session = new UserSession("fixture-session", context.Time.GetUtcNow().AddMinutes(10));
        var target = new TokenExchangeTarget("fixture-audience", Scope: null);

        // Act
        await cache.SetExchangedToken(session, target, source, exchanged, session.ExpiresAt, ct);
        var entry = context.Cache.Entries.ShouldHaveSingleItem();
        var originalProtector = context.DataProtection.CreateProtector("PandaBear.AspNetCore.Authentication.Bff.ExchangedToken.v1")
            .CreateProtector(entry.Key);
        var plaintext = originalProtector.Unprotect(entry.Value);
        await context.Cache.SetAsync(entry.Key, originalProtector.Protect(plaintext), context.Cache.Options[entry.Key], ct);
        var restored = await cache.GetExchangedToken(session, target, source, ct);

        // Assert
        restored.ShouldBe((exchanged, session.ExpiresAt));
    }
}
