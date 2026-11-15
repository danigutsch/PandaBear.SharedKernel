using System.Diagnostics;

namespace PandaBear.Processes.Tests;

/// <summary>Tests the bounded runner with real child processes.</summary>
[Trait(TestTraitNames.CategoryName, TestTraitValues.SecurityCategory)]
[Trait(TestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
public sealed class BoundedProcessRunnerTests
{
    [Fact]
    public async Task Collects_the_output_and_exit_code()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var startInfo = ShellStartInfo.Create("echo out; echo warn >&2; exit 3");

        // Act
        var output = await new BoundedProcessRunner().Run(startInfo, null, TestContext.Current.CancellationToken);

        // Assert
        output.ExitCode.ShouldBe(3);
        output.StandardOutput.ShouldBe("out\n");
        output.StandardError.ShouldBe("warn\n");
        startInfo.RedirectStandardOutput.ShouldBeTrue("The runner redirects the streams it captures.");
    }

    [Fact]
    public async Task Forwards_each_line_and_keeps_no_output()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var forward = new InvariantStringWriter();

        // Act
        var output = await new BoundedProcessRunner().Run(ShellStartInfo.Create("echo first; echo second"), forward, TestContext.Current.CancellationToken);

        // Assert
        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldBeEmpty();
        forward.ToString().ShouldBe("first\n" + "second\n");
    }

    [Fact]
    public async Task Endless_forwarded_output_still_ends_at_the_output_limit()
    {
        // Arrange
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Act
        Func<Task> run = () => new BoundedProcessRunner().Run(ShellStartInfo.Create("yes"), TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        (await run.ShouldThrow<InvalidOperationException>()).Message.ShouldContain("more output than the limit", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_relative_file_name_is_rejected_before_anything_starts()
    {
        // Act
        Func<Task> run = () => new BoundedProcessRunner().Run(new ProcessStartInfo("git"), null, TestContext.Current.CancellationToken);

        // Assert
        await run.ShouldThrow<ArgumentException>();
    }
}
