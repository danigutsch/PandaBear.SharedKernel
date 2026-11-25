namespace SharedKernel.CI.Tests;

/// <summary>Guards the forge-neutral pruning plan and how a failed deletion is reported.</summary>
public sealed class PrereleasePrunerTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_timed_out_deletion_stops_the_run_and_names_what_was_deleted_before_it()
    {
        // Arrange
        var store = new FakeReleaseStore(
            new RepositoryRelease(1, "v1.0.0-alpha.1", false, true, Start.AddDays(1)),
            new RepositoryRelease(2, "v1.0.0-alpha.2", false, true, Start.AddDays(2)),
            new RepositoryRelease(3, "v1.0.0-alpha.3", false, true, Start.AddDays(3)),
            new RepositoryRelease(4, "v1.0.0", false, false, Start.AddDays(4)));
        var timeout = new TimeoutException("DELETE https://forge.example/releases/1 did not finish within 60 seconds.", new OperationCanceledException());
        store.Failures[1] = timeout;
        Func<Task> prune = () => PrereleasePruner.Prune(
            new PrereleasePruningRequest("owner/name", 1, [], Force: true), store, TextWriter.Null, TextWriter.Null, TestContext.Current.CancellationToken);

        // Act
        var exception = await prune.ShouldThrow<HttpRequestException>();

        // Assert
        store.Deleted.ShouldBe([2]);
        exception.Message.ShouldBe(
            "DELETE https://forge.example/releases/1 did not finish within 60 seconds. The release was v1.0.0-alpha.1." + Environment.NewLine
            + "Deleted before the failure: v1.0.0-alpha.2." + Environment.NewLine
            + "No further release was deleted.");
        exception.InnerException.ShouldBeSameAs(timeout.InnerException);
        exception.StatusCode.ShouldBeNull();
    }

    [Fact]
    public async Task The_preview_ignores_stable_releases_and_drafts_and_deletes_nothing()
    {
        // Arrange
        var store = new FakeReleaseStore(
            new RepositoryRelease(1, "v1.0.0-alpha.1", false, true, Start.AddDays(1)),
            new RepositoryRelease(2, "v1.0.0-alpha.2", true, true, Start.AddDays(2)),
            new RepositoryRelease(3, "v1.0.0", false, false, Start.AddDays(3)));
        using var output = new InvariantStringWriter();

        // Act
        await PrereleasePruner.Prune(new PrereleasePruningRequest("owner/name", 1, [], Force: false), store, output, TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        store.Deleted.ShouldBeEmpty();
        output.ToString().ShouldContain("Ignored 1 stable release and 1 draft.", StringComparison.Ordinal);
    }
}
