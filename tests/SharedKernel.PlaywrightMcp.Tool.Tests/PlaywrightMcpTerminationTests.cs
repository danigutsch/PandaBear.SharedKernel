namespace PandaBear.PlaywrightMcp.Tool.Tests;

/// <summary>Tests how the tool process reacts to a termination signal.</summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
public sealed class PlaywrightMcpTerminationTests
{
    /// <summary>Verifies that SIGTERM makes the tool exit 1 with a cancellation error, but only after the container removal finished.</summary>
    [Fact]
    public async Task Termination_signal_waits_for_container_removal_before_exiting()
    {
        // Arrange
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var engine = new FakeDockerEngine(removalDelaySeconds: 3);
        await using var tool = PlaywrightMcpToolProcess.StartServer(engine.DirectoryPath);
        using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        watchdog.CancelAfter(TimeSpan.FromSeconds(60));
        await engine.WaitUntilRuntimeStarts(watchdog.Token);

        // Act
        await tool.SendTerminationSignal(watchdog.Token);
        var result = await tool.WaitForExit(watchdog.Token);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardOutput.ShouldBeEmpty();
        result.StandardError.ShouldBe("Error: Playwright MCP was cancelled." + Environment.NewLine);
        var log = engine.ReadLog();
        log.ShouldContain(" rm --force sharedkernel-playwright-mcp-", StringComparison.Ordinal);
        log.ShouldContain("removed", StringComparison.Ordinal);
    }
}
