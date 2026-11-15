using System.Diagnostics;

namespace PandaBear.Processes.Tests;

/// <summary>Tests the unbounded runner with real child processes.</summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
public sealed class SystemProcessRunnerTests
{
    [Fact]
    public async Task Cancellation_is_preserved_after_terminating_a_running_child()
    {
        // Arrange
        await using var child = new CancellableChildProcess();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        // Act
        var execution = new SystemProcessRunner().Run(child.StartInfo, null, cancellation.Token);
        await child.WaitUntilReady(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        Func<Task> waitForExecution = async () => await execution;

        // Assert
        await waitForExecution.ShouldThrowAssignableTo<OperationCanceledException>();
        child.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task Forwards_each_output_line_while_the_child_runs()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var releasePath = Path.Combine(directory.Path, "release");
        var startInfo = new ProcessStartInfo("/bin/sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(
            "echo pulling; i=0; while [ ! -e \"$1\" ]; do i=$((i + 1)); if [ \"$i\" -gt 300 ]; then exit 3; fi; sleep 0.1; done; echo pulled >&2");
        startInfo.ArgumentList.Add("sh");
        startInfo.ArgumentList.Add(releasePath);
        using var error = new FirstLineWriter();
        var firstLine = error.FirstLine;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        // Act
        // The child exits only after the release file exists, which is created only after its first line reached the writer.
        var release = Task.Run(
            async () =>
            {
                var forwarded = await firstLine.WaitAsync(timeout.Token);
                await File.WriteAllTextAsync(releasePath, string.Empty, timeout.Token);
                return forwarded;
            },
            timeout.Token);
        var result = await new SystemProcessRunner().Run(startInfo, error, timeout.Token);
        var forwardedWhileRunning = await release;

        // Assert
        forwardedWhileRunning.ShouldBe("pulling" + Environment.NewLine);
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBeEmpty();
        result.StandardError.ShouldBeEmpty();
        error.ToString().ShouldBe("pulling" + Environment.NewLine + "pulled" + Environment.NewLine);
    }

    [Fact]
    public async Task A_relative_file_name_is_rejected_before_anything_starts()
    {
        // Act
        Func<Task> run = () => new SystemProcessRunner().Run(new ProcessStartInfo("git"), null, TestContext.Current.CancellationToken);

        // Assert
        await run.ShouldThrow<ArgumentException>();
    }
}
