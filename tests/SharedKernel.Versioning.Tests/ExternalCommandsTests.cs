using System.ComponentModel;
using PandaBear.Processes;

namespace PandaBear.Versioning.Tests;

/// <summary>Tests how the engine runs external programs through the process runner abstraction.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class ExternalCommandsTests
{
    [Fact]
    public static async Task Run_resolves_a_bare_name_and_returns_the_standard_output()
    {
        // Arrange
        var runner = new ScriptedProcessRunner(static _ => new ProcessOutput(0, "abc\n"));
        var commands = new ExternalCommands(runner, runner);

        // Act
        var output = await commands.Run("git", ["rev-parse", "HEAD"], "/repo", TestContext.Current.CancellationToken);

        // Assert
        output.ShouldBe("abc\n");
        var startInfo = runner.Started.ShouldHaveSingleItem();
        startInfo.FileName.ShouldBe("/usr/bin/git");
        startInfo.ArgumentList.ShouldBe(["rev-parse", "HEAD"]);
        startInfo.WorkingDirectory.ShouldBe("/repo");
        startInfo.RedirectStandardOutput.ShouldBeTrue();
        startInfo.RedirectStandardError.ShouldBeTrue();
        runner.Forwarded.ShouldHaveSingleItem().ShouldBeNull("Run collects the output instead of forwarding it.");
    }

    [Fact]
    public static async Task Run_reports_a_non_zero_exit_with_the_requested_command_and_its_output()
    {
        // Arrange
        var runner = new ScriptedProcessRunner(static _ => new ProcessOutput(3, "out", "err"));
        var commands = new ExternalCommands(runner, runner);

        // Act
        Func<Task> action = () => commands.Run("git", ["status", "--porcelain"], null, TestContext.Current.CancellationToken);

        // Assert
        var exception = await action.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe($"Command failed (3): git status --porcelain{Environment.NewLine}out{Environment.NewLine}err");
    }

    [Fact]
    public static async Task Run_reports_an_executable_that_cannot_start_as_an_invalid_operation()
    {
        // Arrange
        var commands = SystemCommands.Instance;
        var missing = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N"));

        // Act
        Func<Task> action = () => commands.Run(missing, [], null, TestContext.Current.CancellationToken);

        // Assert
        var exception = await action.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldStartWith("Failed to start " + missing + ": ", StringComparison.Ordinal);
        exception.InnerException.ShouldBeOfType<Win32Exception>();
    }

    [Fact]
    public static async Task Run_or_default_returns_empty_text_for_a_failed_command()
    {
        // Arrange
        var runner = new ScriptedProcessRunner(static _ => new ProcessOutput(128, string.Empty, "fatal: No names found"));
        var commands = new ExternalCommands(runner, runner);

        // Act
        var output = await commands.RunOrDefault("git", ["describe"], null, TestContext.Current.CancellationToken);

        // Assert
        output.ShouldBeEmpty();
    }

    [Fact]
    public static async Task Run_streaming_applies_the_environment_and_forwards_the_output()
    {
        // Arrange
        var runner = new ScriptedProcessRunner(static _ => new ProcessOutput(0, string.Empty));
        var commands = new ExternalCommands(runner, runner);
        using var sink = new InvariantStringWriter();
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NUGET_PACKAGES"] = "/scratch/packages",
            ["NUGET_FALLBACK_PACKAGES"] = null,
        };

        // Act
        await commands.RunStreaming("dotnet", ["test"], "/clone", environment, sink, TestContext.Current.CancellationToken);

        // Assert
        var startInfo = runner.Started.ShouldHaveSingleItem();
        startInfo.Environment["NUGET_PACKAGES"].ShouldBe("/scratch/packages");
        startInfo.Environment.ContainsKey("NUGET_FALLBACK_PACKAGES").ShouldBeFalse();
        runner.Forwarded.ShouldHaveSingleItem().ShouldBeSameAs(sink);
    }

    [Fact]
    public static async Task Run_streaming_reports_a_non_zero_exit_without_output()
    {
        // Arrange
        var runner = new ScriptedProcessRunner(static _ => new ProcessOutput(1, string.Empty));
        var commands = new ExternalCommands(runner, runner);

        // Act
        Func<Task> action = () => commands.RunStreaming(
            "dotnet", ["test", "--no-build"], "/clone", new Dictionary<string, string?>(), TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        var exception = await action.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldBe("Command failed (1): dotnet test --no-build");
    }
}
