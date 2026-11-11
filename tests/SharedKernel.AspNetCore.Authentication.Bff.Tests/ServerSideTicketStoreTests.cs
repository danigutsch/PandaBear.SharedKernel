using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.Authentication.Bff.Tests;

/// <summary>Tests the protected, distributed cookie ticket store.</summary>
public sealed class ServerSideTicketStoreTests
{
    private static readonly DateTimeOffset Start = TicketStoreTestContext.Start;

    private readonly TicketStoreTestContext _context = new();

    [Fact]
    public async Task A_stored_ticket_round_trips_through_data_protection_under_a_prefixed_random_key()
    {
        // Arrange
        var store = _context.CreateStore();
        var ticket = _context.CreateTicket("alice", expiresIn: TimeSpan.FromHours(1));

        // Act
        var key = await store.StoreAsync(ticket, TestContext.Current.CancellationToken);
        var retrieved = await store.RetrieveAsync(key, TestContext.Current.CancellationToken);

        // Assert
        key.ShouldStartWith("bff-ticket:", StringComparison.Ordinal);
        key.Length.ShouldBeGreaterThan("bff-ticket:".Length + 40);
        retrieved.ShouldNotBeNull();
        retrieved.Principal.FindFirst("sub")!.Value.ShouldBe("alice");
        Encoding.UTF8.GetString(_context.Cache.Entries[key]).ShouldNotContain("alice", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_stored_ticket_gets_its_own_key()
    {
        // Arrange
        var store = _context.CreateStore();

        // Act
        var first = await store.StoreAsync(_context.CreateTicket("alice"), TestContext.Current.CancellationToken);
        var second = await store.StoreAsync(_context.CreateTicket("alice"), TestContext.Current.CancellationToken);

        // Assert
        first.ShouldNotBe(second);
    }

    [Fact]
    public async Task A_ticket_copied_to_another_key_cannot_be_read_and_is_removed()
    {
        // Arrange
        var store = _context.CreateStore();
        var alice = await store.StoreAsync(_context.CreateTicket("alice"), TestContext.Current.CancellationToken);
        var bob = await store.StoreAsync(_context.CreateTicket("bob"), TestContext.Current.CancellationToken);
        _context.Cache.Entries[bob] = _context.Cache.Entries[alice];

        // Act
        var retrieved = await store.RetrieveAsync(bob, TestContext.Current.CancellationToken);

        // Assert
        retrieved.ShouldBeNull();
        _context.Cache.Entries.ContainsKey(bob).ShouldBeFalse();
    }

    [Fact]
    public async Task A_ticket_protected_by_another_application_cannot_be_read()
    {
        // Arrange
        var store = _context.CreateStore();
        var key = await store.StoreAsync(_context.CreateTicket("alice"), TestContext.Current.CancellationToken);
        var otherApplication = _context.CreateStore(dataProtection: new EphemeralDataProtectionProvider());

        // Act
        var retrieved = await otherApplication.RetrieveAsync(key, TestContext.Current.CancellationToken);

        // Assert
        retrieved.ShouldBeNull();
    }

    [Fact]
    public async Task The_cache_entry_expires_with_the_ticket_and_slides_when_configured()
    {
        // Arrange
        var store = _context.CreateStore(options =>
        {
            options.AbsoluteLifetime = TimeSpan.FromHours(8);
            options.SlidingExpiration = TimeSpan.FromMinutes(20);
        });

        // Act
        var key = await store.StoreAsync(_context.CreateTicket("alice", expiresIn: TimeSpan.FromHours(1)), TestContext.Current.CancellationToken);

        // Assert
        _context.Cache.Options[key].AbsoluteExpiration.ShouldBe(Start.AddHours(1));
        _context.Cache.Options[key].SlidingExpiration.ShouldBe(TimeSpan.FromMinutes(20));
    }

    [Fact]
    public async Task A_ticket_without_expiry_is_capped_by_the_absolute_lifetime()
    {
        // Arrange
        var store = _context.CreateStore(options => options.AbsoluteLifetime = TimeSpan.FromHours(2));

        // Act
        var key = await store.StoreAsync(_context.CreateTicket("alice", expiresIn: null), TestContext.Current.CancellationToken);

        // Assert
        _context.Cache.Options[key].AbsoluteExpiration.ShouldBe(Start.AddHours(2));
    }

    [Fact]
    public async Task An_expired_ticket_is_not_returned_and_is_removed()
    {
        // Arrange
        var store = _context.CreateStore();
        var key = await store.StoreAsync(_context.CreateTicket("alice", expiresIn: TimeSpan.FromMinutes(5)), TestContext.Current.CancellationToken);
        _context.Time.Advance(TimeSpan.FromMinutes(6));

        // Act
        var retrieved = await store.RetrieveAsync(key, TestContext.Current.CancellationToken);

        // Assert
        retrieved.ShouldBeNull();
        _context.Cache.Entries.ContainsKey(key).ShouldBeFalse();
    }

    [Fact]
    public async Task Sliding_renewals_never_extend_a_session_past_the_absolute_lifetime()
    {
        // Arrange
        var store = _context.CreateStore(options => options.AbsoluteLifetime = TimeSpan.FromHours(1));
        var key = await store.StoreAsync(_context.CreateTicket("alice", expiresIn: TimeSpan.FromMinutes(55)), TestContext.Current.CancellationToken);
        _context.Time.Advance(TimeSpan.FromMinutes(50));
        var renewed = await store.RetrieveAsync(key, TestContext.Current.CancellationToken);
        renewed!.Properties.IssuedUtc = _context.Time.GetUtcNow();
        renewed.Properties.ExpiresUtc = _context.Time.GetUtcNow().AddMinutes(30);

        // Act
        await store.RenewAsync(key, renewed, TestContext.Current.CancellationToken);
        var capped = _context.Cache.Options[key].AbsoluteExpiration;
        _context.Time.Advance(TimeSpan.FromMinutes(11));
        var afterLifetime = await store.RetrieveAsync(key, TestContext.Current.CancellationToken);

        // Assert
        capped.ShouldBe(Start.AddHours(1));
        afterLifetime.ShouldBeNull();
    }

    [Fact]
    public async Task Removing_a_ticket_deletes_its_cache_entry()
    {
        // Arrange
        var store = _context.CreateStore();
        var key = await store.StoreAsync(_context.CreateTicket("alice"), TestContext.Current.CancellationToken);

        // Act
        await store.RemoveAsync(key, TestContext.Current.CancellationToken);

        // Assert
        _context.Cache.Entries.ShouldBeEmpty();
        var retrieved = await store.RetrieveAsync(key, TestContext.Current.CancellationToken);
        retrieved.ShouldBeNull();
    }

    [Fact]
    public void The_registration_sets_the_session_store_of_the_named_cookie_scheme()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddAuthentication().AddCookie().AddCookie("other");
        services.AddServerSideTicketStore(configure: options => options.KeyPrefix = "app:");
        using var provider = services.BuildServiceProvider();

        // Act
        var monitor = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();

        // Assert
        monitor.Get(CookieAuthenticationDefaults.AuthenticationScheme).SessionStore.ShouldBeOfType<ServerSideTicketStore>();
        monitor.Get("other").SessionStore.ShouldBeNull();
        provider.GetRequiredService<IOptions<ServerSideTicketStoreOptions>>().Value.KeyPrefix.ShouldBe("app:");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_key_prefix_is_rejected(string prefix)
    {
        // Arrange
        Action create = () => _context.CreateStore(options => options.KeyPrefix = prefix);

        // Act & Assert
        create.ShouldThrow<OptionsValidationException>();
    }
}
