namespace PandaBear.CI.Tests;

/// <summary>Guards how a Playwright installation that does not finish is stopped.</summary>
public sealed class PlaywrightInstallationTests : IDisposable
{
    private const string Project = "tests/Core.Tests/Core.Tests.csproj";

    private readonly RepositoryTestScope scope = new();

    [Fact]
    public async Task A_stalled_installer_is_stopped_with_a_timeout()
    {
        // Arrange
        scope.WriteFile("tests/Core.Tests/bin/Debug/net10.0/playwright.ps1", "# installer");

        // Act
        var action = async () => await PlaywrightInstallation.Run(
            scope.Root,
            Project,
            [],
            false,
            null,
            async (fileName, _, _, ct) =>
            {
                if (fileName == "dotnet")
                {
                    return "bin/Debug/net10.0/";
                }

                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return string.Empty;
            },
            TestContext.Current.CancellationToken,
            TimeSpan.FromMilliseconds(100));

        // Assert
        (await action.ShouldThrow<TimeoutException>()).Message.ShouldContain("did not finish within", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public void Dispose() => scope.Dispose();
}
