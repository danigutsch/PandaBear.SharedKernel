using PandaBear.Aspire.Hosting.Umami.Testing;

namespace PandaBear.Aspire.Hosting.Umami.Tests;

/// <summary>Tests UmamiTestWebsiteSource, which borrows the website named in the environment or starts an Umami once on first demand.</summary>
public sealed class UmamiTestWebsiteSourceTests
{
    private static readonly UmamiTestWebsiteConnection Owned = new("Endpoint=http://localhost:3000;WebsiteId=0b5c9b7e-2f4d-5a8c-8f1e-6d3a9c2b7e40", new Uri("http://localhost:3000"), Guid.Parse("0b5c9b7e-2f4d-5a8c-8f1e-6d3a9c2b7e40"));

    [Fact]
    public async Task A_set_variable_borrows_the_website_and_never_starts_an_umami()
    {
        // Arrange
        var starts = 0;
        await using var source = new UmamiTestWebsiteSource(
            _ =>
            {
                Interlocked.Increment(ref starts);
                return Task.FromResult(new StartedUmamiTestWebsite(Owned, () => ValueTask.CompletedTask));
            },
            UmamiTestEnvironment.WithWebsite(UmamiTestEnvironment.Website));

        // Act
        var website = await source.Get(TestContext.Current.CancellationToken);

        // Assert
        website.ConnectionString.ShouldBe(UmamiTestEnvironment.Website);
        starts.ShouldBe(0, "a shared Umami runs once per test run, so a borrower never starts a second one");
    }

    [Fact]
    public async Task Without_the_variable_concurrent_demands_start_one_umami_and_disposal_stops_it_once()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var starts = 0;
        var stops = 0;
        var started = new TaskCompletionSource<StartedUmamiTestWebsite>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new UmamiTestWebsiteSource(
            _ =>
            {
                Interlocked.Increment(ref starts);
                return started.Task;
            },
            UmamiTestEnvironment.WithWebsite(null));

        // Act
        var demands = Enumerable.Range(0, 8).Select(_ => Task.Run(() => source.Get(ct), ct)).ToArray();
        started.SetResult(new StartedUmamiTestWebsite(Owned, () =>
        {
            Interlocked.Increment(ref stops);
            return ValueTask.CompletedTask;
        }));
        var websites = await Task.WhenAll(demands);
        await source.DisposeAsync();

        // Assert
        starts.ShouldBe(1, "every demand of the assembly shares the first start");
        websites.ShouldAllSatisfy(website => website.ShouldBeSameAs(Owned));
        stops.ShouldBe(1);
    }

    [Fact]
    public async Task A_failed_start_is_cached_and_reaches_every_later_demand_without_another_start()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var starts = 0;
        var source = new UmamiTestWebsiteSource(
            _ =>
            {
                Interlocked.Increment(ref starts);
                return Task.FromException<StartedUmamiTestWebsite>(new TimeoutException("Umami was not healthy."));
            },
            UmamiTestEnvironment.WithWebsite(null));

        // Act
        Func<Task> first = () => source.Get(ct);
        Func<Task> second = () => source.Get(ct);
        var firstFailure = await first.ShouldThrow<TimeoutException>();
        var secondFailure = await second.ShouldThrow<TimeoutException>();
        await source.DisposeAsync();

        // Assert
        starts.ShouldBe(1, "a startup timeout is paid once per assembly, not once per test");
        secondFailure.ShouldBeSameAs(firstFailure);
    }

    [Fact]
    public async Task A_malformed_variable_fails_every_demand_and_starts_nothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var starts = 0;
        await using var source = new UmamiTestWebsiteSource(
            _ =>
            {
                Interlocked.Increment(ref starts);
                return Task.FromResult(new StartedUmamiTestWebsite(Owned, () => ValueTask.CompletedTask));
            },
            UmamiTestEnvironment.WithWebsite(UmamiTestWebsite.Unavailable));

        // Act
        Func<Task> first = () => source.Get(ct);
        Func<Task> second = () => source.Get(ct);
        _ = await first.ShouldThrow<InvalidOperationException>();
        _ = await second.ShouldThrow<InvalidOperationException>();

        // Assert
        starts.ShouldBe(0, "a broken owner must fail the tests, not be hidden behind a second Umami");
    }

    [Fact]
    public async Task A_source_that_was_never_asked_reads_and_starts_nothing()
    {
        // Arrange
        var starts = 0;
        var source = new UmamiTestWebsiteSource(
            _ =>
            {
                Interlocked.Increment(ref starts);
                return Task.FromResult(new StartedUmamiTestWebsite(Owned, () => ValueTask.CompletedTask));
            },
            _ => throw new InvalidOperationException("The environment must not be read before a test needs the website."));

        // Act
        await source.DisposeAsync();

        // Assert
        starts.ShouldBe(0, "an assembly whose selected tests need no Umami pays for no container");
    }
}
