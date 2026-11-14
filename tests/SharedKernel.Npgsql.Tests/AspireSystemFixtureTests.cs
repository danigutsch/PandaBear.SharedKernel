namespace PandaBear.Npgsql.Tests;

/// <summary>Tests AspireSystemFixture: one application per profile, started on demand, each in its own workspace folder, removed on disposal.</summary>
public sealed class AspireSystemFixtureTests
{
    [Fact]
    public async Task A_profile_starts_once_in_its_own_folder_and_disposal_removes_the_workspace()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var fixture = new TalkingSystemFixture();

        // Act
        var first = await fixture.Application("first", ct);
        var again = await fixture.Application("first", ct);
        var second = await fixture.Application("second", ct);
        var root = fixture.Workspace.Root;
        await fixture.DisposeAsync();

        // Assert
        again.ShouldBeSameAs(first, "a profile starts once and every caller shares it");
        second.ShouldNotBeSameAs(first);
        fixture.Folders.Order(StringComparer.Ordinal).ToList().ShouldBe([Path.Combine(root, "first"), Path.Combine(root, "second")]);
        Directory.Exists(root).ShouldBeFalse("disposal must remove the workspace");
    }

    [Fact]
    public async Task A_fixture_that_no_test_used_starts_nothing()
    {
        // Arrange
        var fixture = new TalkingSystemFixture();

        // Act
        await fixture.DisposeAsync();

        // Assert
        fixture.Folders.ShouldBeEmpty("no profile was asked for");
    }

    [Fact]
    public async Task A_workspace_that_cannot_be_removed_is_reported_together_with_the_stop_failure()
    {
        // Arrange
        var fixture = new UndeletableWorkspaceFixture();
        using var abandon = new CancellationTokenSource();
        var pending = fixture.Application("held", abandon.Token);
        await abandon.CancelAsync();
        await Task.WhenAny(pending);
        var startFailure = new InvalidOperationException("start failed");
        fixture.FailStart(startFailure);

        // Act
        var disposal = fixture.DisposeAsync().AsTask();
        await Task.WhenAny(disposal);
        fixture.Unlock();

        // Assert
        var failures = disposal.Exception.ShouldNotBeNull().Flatten().InnerExceptions;
        failures.ShouldContain(startFailure);
        failures.ShouldContain(failure => failure is IOException or UnauthorizedAccessException);
    }
}
