using System.Diagnostics;

namespace SharedKernel.PlaywrightMcp.Tool.Tests;

[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
public sealed class CancellableChildProcessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Child_reports_its_identity_and_remains_running_until_disposed(bool disableModuleAutoloading)
    {
        // Arrange
        var child = new CancellableChildProcess();
        if (disableModuleAutoloading && OperatingSystem.IsWindows())
        {
            var arguments = child.StartInfo.ArgumentList;
            arguments[^1] = "$PSModuleAutoLoadingPreference = 'None'; " + arguments[^1];
        }

        using var process = new Process { StartInfo = child.StartInfo };
        await using (child)
        {
            // Act
            process.Start().ShouldBeTrue();
            var readiness = child.WaitUntilReady(TestContext.Current.CancellationToken);
            await Task.WhenAny(readiness, process.WaitForExitAsync(TestContext.Current.CancellationToken));
            if (process.HasExited)
            {
                var error = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
                throw new InvalidOperationException($"Child exited before readiness. Standard error: {error}");
            }

            await readiness;

            // Assert
            child.IsRunning.ShouldBeTrue();
        }

        child.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task Readiness_preserves_caller_cancellation()
    {
        // Arrange
        await using var child = new CancellableChildProcess();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        Func<Task> waitForReadiness = () => child.WaitUntilReady(cancellation.Token);

        // Assert
        var exception = await waitForReadiness.ShouldThrowAssignableTo<OperationCanceledException>();
        exception.CancellationToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public void Reused_process_ids_do_not_match_the_original_child_identity()
    {
        // Arrange
        using var process = Process.GetCurrentProcess();
        var differentStartTime = process.StartTime.ToUniversalTime().AddTicks(1);

        // Act
        var matches = CancellableChildProcess.MatchesIdentity(process, differentStartTime);

        // Assert
        matches.ShouldBeFalse();
    }
}
