using System.Data;
using PandaBear.Npgsql;
using PandaBear.DispositionHold;
using PandaBear.DispositionHold.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// PostgreSQL tests of the disposition hold store and guard: fail-closed behavior, decisions and lock ordering.
/// </summary>
/// <param name="fixture">Shared PostgreSQL fixture that supplies the isolated database.</param>
[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.DatabaseIntegrationCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.DispositionHoldCapability)]
public sealed class PostgreSqlDispositionHoldTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Placed = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan BlockedProbe = TimeSpan.FromMilliseconds(500);

    private PostgreSqlDispositionHoldScenario? scenario;

    private PostgreSqlDispositionHoldScenario Scenario =>
        scenario ?? throw new InvalidOperationException("Test scenario is not initialized.");

    /// <summary>Creates the PostgreSQL hold scenario for the test.</summary>
    public async ValueTask InitializeAsync()
    {
        scenario = await PostgreSqlDispositionHoldScenario.Create(fixture, TestContext.Current.CancellationToken);
    }

    /// <summary>Releases the PostgreSQL hold scenario.</summary>
    public async ValueTask DisposeAsync()
    {
        if (scenario is not null)
        {
            await scenario.DisposeAsync();
        }
    }

    [Fact]
    public async Task Check_without_an_open_transaction_refuses_instead_of_clearing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var caller = await Scenario.Open(beginTransaction: false, ct);
        var scopes = new[] { DispositionScope.From("document", "doc-1") };

        // Act
        var check = async () => await caller.Guard.Check(scopes, Placed, ct);

        // Assert
        _ = await check.ShouldThrow<DispositionHoldStateUnknownException>();
    }

    [Theory]
    [InlineData(IsolationLevel.RepeatableRead)]
    [InlineData(IsolationLevel.Serializable)]
    public async Task Check_refuses_when_the_transaction_isolation_could_hide_a_hold_committed_while_waiting(IsolationLevel isolationLevel)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var purge = await Scenario.Open(beginTransaction: true, ct, isolationLevel);

        // Act
        var check = async () => await purge.Guard.Check([DispositionScope.From("document", "doc-1")], Placed, ct);

        // Assert
        _ = await check.ShouldThrow<DispositionHoldStateUnknownException>();
    }

    [Fact]
    public async Task Check_sees_a_hold_committed_while_it_waited_even_after_an_earlier_query_in_the_transaction()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scope = DispositionScope.From("document", "doc-1");
        var hold = DispositionHoldTestData.Place(scope);
        await using var placer = await Scenario.Open(beginTransaction: true, ct);
        await using var purge = await Scenario.Open(beginTransaction: true, ct);
        await placer.Store.Add(hold, ct);
        await purge.RunAnyQuery(ct);

        // Act
        var check = purge.Guard.Check([scope], Placed.AddDays(2), ct).AsTask();
        await Task.WhenAny(check, Task.Delay(BlockedProbe, ct));
        await placer.Commit(ct);
        var decision = await check.WaitAsync(ct);

        // Assert
        decision.HoldIds.ShouldBe([hold.Id]);
    }

    [Fact]
    public async Task Check_with_no_scopes_still_refuses_without_an_open_transaction()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var caller = await Scenario.Open(beginTransaction: false, ct);

        // Act
        var check = async () => await caller.Guard.Check([], Placed, ct);

        // Assert
        _ = await check.ShouldThrow<DispositionHoldStateUnknownException>();
    }

    [Fact]
    public async Task Add_without_an_open_transaction_refuses()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var caller = await Scenario.Open(beginTransaction: false, ct);

        // Act
        var add = async () => await caller.Store.Add(DispositionHoldTestData.Place(), ct);

        // Assert
        _ = await add.ShouldThrow<DispositionHoldStateUnknownException>();
    }

    [Fact]
    public async Task Check_reports_an_effective_unreleased_hold_on_a_requested_scope()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var held = DispositionScope.From("document", "doc-1");
        var hold = DispositionHoldTestData.Place(held);
        await using (var placer = await Scenario.Open(beginTransaction: true, ct))
        {
            await placer.Store.Add(hold, ct);
            await placer.Commit(ct);
        }

        await using var purge = await Scenario.Open(beginTransaction: true, ct);

        // Act
        var decision = await purge.Guard.Check([DispositionScope.From("document", "doc-2"), held], Placed.AddDays(2), ct);

        // Assert
        decision.IsClear.ShouldBeFalse();
        decision.HoldIds.ShouldBe([hold.Id]);
    }

    [Fact]
    public async Task Check_is_clear_before_effect_after_expiry_after_release_and_for_other_scopes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var held = DispositionScope.From("document", "doc-1");
        var expiring = DispositionHoldTestData.Place(held, expiresAt: Placed.AddDays(10));
        var released = DispositionHoldTestData.Place(DispositionScope.From("document", "doc-2"));
        released.Release("actor-2", "claim-closed", Placed.AddDays(3));
        await using (var placer = await Scenario.Open(beginTransaction: true, ct))
        {
            await placer.Store.Add(expiring, ct);
            await placer.Store.Add(released, ct);
            await placer.Commit(ct);
        }

        await using var purge = await Scenario.Open(beginTransaction: true, ct);

        // Act
        var beforeEffect = await purge.Guard.Check([held], Placed, ct);
        await purge.Restart(ct);
        var afterExpiry = await purge.Guard.Check([held], Placed.AddDays(10), ct);
        await purge.Restart(ct);
        var afterRelease = await purge.Guard.Check([released.Scope], Placed.AddDays(5), ct);
        await purge.Restart(ct);
        var otherScope = await purge.Guard.Check([DispositionScope.From("document", "doc-3")], Placed.AddDays(2), ct);

        // Assert
        beforeEffect.IsClear.ShouldBeTrue();
        afterExpiry.IsClear.ShouldBeTrue();
        afterRelease.IsClear.ShouldBeTrue();
        otherScope.IsClear.ShouldBeTrue();
    }

    [Fact]
    public async Task Add_waits_for_a_purge_that_is_already_checking_the_same_scope()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scope = DispositionScope.From("document", "doc-1");
        await using var purge = await Scenario.Open(beginTransaction: true, ct);
        await using var placer = await Scenario.Open(beginTransaction: true, ct);
        var decision = await purge.Guard.Check([scope], Placed.AddDays(2), ct);

        // Act
        var add = placer.Store.Add(DispositionHoldTestData.Place(scope), ct).AsTask();
        var finishedWhilePurgeOpen = await Task.WhenAny(add, Task.Delay(BlockedProbe, ct)) == add;
        await purge.Commit(ct);
        await add.WaitAsync(ct);

        // Assert
        decision.IsClear.ShouldBeTrue();
        finishedWhilePurgeOpen.ShouldBeFalse();
    }

    [Fact]
    public async Task Check_waits_for_a_hold_that_is_being_placed_and_then_sees_it()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scope = DispositionScope.From("document", "doc-1");
        var hold = DispositionHoldTestData.Place(scope);
        await using var placer = await Scenario.Open(beginTransaction: true, ct);
        await using var purge = await Scenario.Open(beginTransaction: true, ct);
        await placer.Store.Add(hold, ct);

        // Act
        var check = purge.Guard.Check([scope], Placed.AddDays(2), ct).AsTask();
        var finishedWhilePlacing = await Task.WhenAny(check, Task.Delay(BlockedProbe, ct)) == check;
        await placer.Commit(ct);
        var decision = await check.WaitAsync(ct);

        // Assert
        finishedWhilePlacing.ShouldBeFalse();
        decision.HoldIds.ShouldBe([hold.Id]);
    }

    [Fact]
    public async Task Check_refuses_a_later_call_in_the_same_transaction_that_would_take_a_smaller_lock_key()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scopes = new[] { DispositionScope.From("document", "doc-1"), DispositionScope.From("document", "doc-2") }
            .OrderBy(scope => PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian(HoldScopeLock.LockName(scope)))
            .ToArray();
        await using var purge = await Scenario.Open(beginTransaction: true, ct);
        _ = await purge.Guard.Check([scopes[1]], Placed, ct);

        // Act
        var check = async () => await purge.Guard.Check([scopes[0]], Placed, ct);

        // Assert
        var exception = await check.ShouldThrow<DispositionHoldStateUnknownException>();
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task Check_accepts_the_same_scopes_again_and_a_fresh_order_in_a_new_transaction()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scopes = new[] { DispositionScope.From("document", "doc-1"), DispositionScope.From("document", "doc-2") }
            .OrderBy(scope => PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian(HoldScopeLock.LockName(scope)))
            .ToArray();
        await using var purge = await Scenario.Open(beginTransaction: true, ct);
        _ = await purge.Guard.Check([scopes[1]], Placed, ct);
        var again = await purge.Guard.Check([scopes[1]], Placed, ct);
        await purge.Restart(ct);

        // Act
        var afterRestart = await purge.Guard.Check([scopes[0]], Placed, ct);

        // Assert
        again.IsClear.ShouldBeTrue();
        afterRestart.IsClear.ShouldBeTrue();
    }

    [Fact]
    public async Task Checks_over_the_same_scopes_in_opposite_order_do_not_deadlock()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var first = DispositionScope.From("document", "doc-1");
        var second = DispositionScope.From("document", "doc-2");
        await using var left = await Scenario.Open(beginTransaction: true, ct);
        await using var right = await Scenario.Open(beginTransaction: true, ct);

        // Act
        var leftChecks = left.CheckRepeatedly([first, second], Placed.AddDays(2), rounds: 25, ct);
        var rightChecks = right.CheckRepeatedly([second, first], Placed.AddDays(2), rounds: 25, ct);
        var both = Task.WhenAll(leftChecks, rightChecks);

        // Assert
        await both.WaitAsync(TimeSpan.FromSeconds(30), ct);
    }
}
