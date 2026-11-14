using System.Collections.Concurrent;
using PandaBear.IntegrationTesting;

namespace PandaBear.Npgsql.Tests;

/// <summary>Tests OnDemandResources, a set of on-demand resources keyed by name, each started once on its first demand.</summary>
public sealed class OnDemandResourcesTests
{
    [Fact]
    public async Task Each_key_starts_once_and_keys_never_share_a_value()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var starts = new ConcurrentBag<string>();
        await using var resources = new OnDemandResources<string>((key, _) =>
        {
            starts.Add(key);
            return Task.FromResult("value of " + key);
        });

        // Act
        var first = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => resources.Get("cache", ct)));
        var other = await resources.Get("database", ct);

        // Assert
        first.ShouldAllSatisfy(value => value.ShouldBe("value of cache"), "every caller of one key shares its value");
        other.ShouldBe("value of database");
        starts.Order(StringComparer.Ordinal).ToList().ShouldBe(["cache", "database"], "each key must start exactly once");
    }

    [Fact]
    public async Task Disposal_disposes_every_started_value_and_refuses_later_demands()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var started = new AsyncDisposableMarker();
        var resources = new OnDemandResources<AsyncDisposableMarker>((_, _) => Task.FromResult(started));
        _ = await resources.Get("started", ct);
        await resources.DisposeAsync();
        Func<Task> afterDisposal = () => resources.Get("later", ct);

        // Act
        _ = await afterDisposal.ShouldThrow<ObjectDisposedException>();

        // Assert
        started.DisposeCount.ShouldBe(1, "a started value must be disposed once");
    }

    [Fact]
    public async Task A_demand_that_races_disposal_is_refused_or_its_value_is_disposed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var leaked = 0;

        // Act
        for (var attempt = 0; attempt < 2000; attempt++)
        {
            var started = new ConcurrentBag<AsyncDisposableMarker>();
            await using var resources = new OnDemandResources<AsyncDisposableMarker>((_, _) =>
            {
                var marker = new AsyncDisposableMarker();
                started.Add(marker);
                return Task.FromResult(marker);
            });
            using var gate = new Barrier(2);
            var demand = Task.Run(
                () =>
                {
                    gate.SignalAndWait(ct);
                    return resources.Get("racing", ct);
                },
                ct);
            var disposal = Task.Run(
                async () =>
                {
                    gate.SignalAndWait(ct);
                    await resources.DisposeAsync();
                },
                ct);
            await disposal;
            await Task.WhenAny(demand);
            if (demand.IsFaulted)
            {
                demand.Exception.InnerException.ShouldBeOfType<ObjectDisposedException>();
            }

            leaked += started.Count(marker => marker.DisposeCount == 0);
        }

        // Assert
        leaked.ShouldBe(0, "a value started while the set was disposed must be disposed too");
    }

    [Fact]
    public async Task A_blank_key_is_refused()
    {
        // Arrange
        await using var resources = new OnDemandResources<string>((key, _) => Task.FromResult(key));
        Func<Task> blank = () => resources.Get(" ", TestContext.Current.CancellationToken);

        // Act
        var refused = await blank.ShouldThrow<ArgumentException>();

        // Assert
        refused.ParamName.ShouldBe("key");
    }
}
