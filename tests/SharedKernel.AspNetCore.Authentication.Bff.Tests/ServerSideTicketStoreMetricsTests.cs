using System.Security.Cryptography;

namespace SharedKernel.AspNetCore.Authentication.Bff.Tests;

/// <summary>Tests the ticket store's failure counter and its bounded dimensions.</summary>
[Collection(SerialTicketStoreMetrics.Name)]
public sealed class ServerSideTicketStoreMetricsTests
{
    private const string TicketKey = "private-ticket-key";
    private const string Subject = "private-ticket-subject";
    private const string FailureMessage = "private-exception-message";

    [Fact]
    public async Task Protect_failure_counts_only_the_operation_and_rethrows_the_same_error()
    {
        // Arrange
        using var metrics = new TicketStoreMetricsListener();
        var context = new TicketStoreTestContext();
        var failure = new CryptographicException(FailureMessage);
        var protector = new TicketStoreTestProtector(_ => throw failure, static bytes => bytes);
        var store = context.CreateStore(dataProtection: protector);
        CancellationToken ct = TestContext.Current.CancellationToken;
        Func<Task> renew = () => store.RenewAsync(TicketKey, context.CreateTicket(Subject), ct);

        // Act
        var error = await renew.ShouldThrow<CryptographicException>();

        // Assert
        error.ShouldBeSameAs(failure);
        context.Cache.Entries.ShouldBeEmpty();
        metrics.ShouldHaveSingleFailure("protect");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unreadable_tickets_count_as_unprotect_failures_and_are_removed(bool throws)
    {
        // Arrange
        using var metrics = new TicketStoreMetricsListener();
        var context = new TicketStoreTestContext();
        context.Cache.Entries[TicketKey] = BitConverter.GetBytes(-1);
        var protector = new TicketStoreTestProtector(static bytes => bytes, bytes => throws ? throw new CryptographicException(FailureMessage) : bytes);
        var store = context.CreateStore(dataProtection: protector);

        // Act
        var ticket = await store.RetrieveAsync(TicketKey, TestContext.Current.CancellationToken);

        // Assert
        ticket.ShouldBeNull();
        context.Cache.Entries.ContainsKey(TicketKey).ShouldBeFalse();
        metrics.ShouldHaveSingleFailure("unprotect");
    }

    [Fact]
    public async Task Unexpected_protection_failure_is_counted_and_propagated_without_removing_the_ticket()
    {
        // Arrange
        using var metrics = new TicketStoreMetricsListener();
        var context = new TicketStoreTestContext();
        context.Cache.Entries[TicketKey] = BitConverter.GetBytes(-1);
        var failure = new InvalidOperationException(FailureMessage);
        var protector = new TicketStoreTestProtector(static bytes => bytes, _ => throw failure);
        var store = context.CreateStore(dataProtection: protector);
        Func<Task> retrieve = () => store.RetrieveAsync(TicketKey, TestContext.Current.CancellationToken);

        // Act
        var error = await retrieve.ShouldThrow<InvalidOperationException>();

        // Assert
        error.ShouldBeSameAs(failure);
        context.Cache.Entries.ContainsKey(TicketKey).ShouldBeTrue();
        metrics.ShouldHaveSingleFailure("unprotect");
    }

    [Theory]
    [InlineData("cache_get")]
    [InlineData("cache_set")]
    [InlineData("cache_remove")]
    public async Task Cache_failures_count_only_the_operation_and_rethrow_the_same_error(string operation)
    {
        // Arrange
        using var metrics = new TicketStoreMetricsListener();
        var context = new TicketStoreTestContext();
        var store = context.CreateStore();
        var failure = new IOException(FailureMessage);
        context.Cache.GetFailure = failure;
        context.Cache.SetFailure = failure;
        context.Cache.RemoveOverride = () => Task.FromException(failure);
        CancellationToken ct = TestContext.Current.CancellationToken;
        Func<Task> call = operation switch
        {
            "cache_get" => () => store.RetrieveAsync(TicketKey, ct),
            "cache_set" => () => store.RenewAsync(TicketKey, context.CreateTicket(Subject), ct),
            _ => () => store.RemoveAsync(TicketKey, ct),
        };

        // Act
        var error = await call.ShouldThrow<IOException>();

        // Assert
        error.ShouldBeSameAs(failure);
        metrics.ShouldHaveSingleFailure(operation);
    }

    [Fact]
    public void Synchronous_cache_remove_failure_stays_synchronous()
    {
        // Arrange
        using var metrics = new TicketStoreMetricsListener();
        var context = new TicketStoreTestContext();
        var store = context.CreateStore();
        var failure = new IOException(FailureMessage);
        context.Cache.RemoveOverride = () => throw failure;
        Action remove = () => store.RemoveAsync(TicketKey, TestContext.Current.CancellationToken);

        // Act
        var error = remove.ShouldThrow<IOException>();

        // Assert
        error.ShouldBeSameAs(failure);
        metrics.ShouldHaveSingleFailure("cache_remove");
    }

    [Fact]
    public async Task Successful_operations_normal_misses_expired_tickets_and_cancellation_emit_no_failure()
    {
        // Arrange
        using var metrics = new TicketStoreMetricsListener();
        var context = new TicketStoreTestContext();
        var store = context.CreateStore();
        CancellationToken ct = TestContext.Current.CancellationToken;
        string key = await store.StoreAsync(context.CreateTicket(Subject, TimeSpan.FromMinutes(1)), ct);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Func<Task> cancelledRead = () => store.RetrieveAsync(TicketKey, cancellation.Token);

        // Act
        var present = await store.RetrieveAsync(key, ct);
        context.Time.Advance(TimeSpan.FromMinutes(2));
        var expired = await store.RetrieveAsync(key, ct);
        await store.RenewAsync(key, context.CreateTicket(Subject, TimeSpan.Zero), ct);
        var missing = await store.RetrieveAsync(TicketKey, ct);
        await cancellation.CancelAsync();
        context.Cache.GetFailure = new OperationCanceledException(cancellation.Token);
        var cancelled = await cancelledRead.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        present.ShouldNotBeNull();
        expired.ShouldBeNull();
        context.Cache.Entries.ContainsKey(key).ShouldBeFalse();
        missing.ShouldBeNull();
        cancelled.CancellationToken.ShouldBe(cancellation.Token);
        metrics.ShouldHaveNoMeasurements();
    }

    [Fact]
    public async Task A_metrics_listener_failure_preserves_unreadable_ticket_cleanup()
    {
        // Arrange
        using var metrics = new TicketStoreMetricsListener(throwOnMeasurement: true);
        var context = new TicketStoreTestContext();
        context.Cache.Entries[TicketKey] = BitConverter.GetBytes(-1);
        var protector = new TicketStoreTestProtector(static bytes => bytes, static bytes => bytes);
        var store = context.CreateStore(dataProtection: protector);

        // Act
        var ticket = await store.RetrieveAsync(TicketKey, TestContext.Current.CancellationToken);

        // Assert
        ticket.ShouldBeNull();
        context.Cache.Entries.ContainsKey(TicketKey).ShouldBeFalse();
        metrics.ShouldHaveSingleFailure("unprotect");
    }
}
