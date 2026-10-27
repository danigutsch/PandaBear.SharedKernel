using System.CommandLine.Help;
using System.CommandLine.Parsing;

namespace PandaBear.K6.Tool.Tests;

/// <summary>Tests parsing and help of the k6 command line.</summary>
public sealed class K6CommandLineTests
{
    [Theory]
    [InlineData("run")]
    [InlineData("run --scenario smoke --image " + K6TestRepository.Image)]
    [InlineData("run --scenario smoke --container --image registry.example/k6:1.0")]
    [InlineData("run --scenario smoke --container --image -registry.example/k6@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("run --scenario smoke --root")]
    public async Task Invalid_command_lines_exit_1_without_running(string commandLine)
    {
        // Arrange
        using var repository = new K6TestRepository();

        // Act
        var exitCode = await repository.Invoke(CommandLineParser.SplitCommandLine(commandLine).ToArray(), TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        repository.Error.ToString().ShouldNotBe(string.Empty);
        repository.RunDirectories().ShouldBe([]);
    }

    [Theory]
    [InlineData("--image requires --container.", "run --scenario smoke --image " + K6TestRepository.Image)]
    [InlineData("--image requires an image reference pinned by digest, such as grafana/k6:<tag>@sha256:<digest>.", "run --scenario smoke --container --image registry.example/k6:1.0")]
    public async Task Image_errors_name_the_rule(string message, string commandLine)
    {
        // Arrange
        using var repository = new K6TestRepository();

        // Act
        await repository.Invoke(CommandLineParser.SplitCommandLine(commandLine).ToArray(), TestContext.Current.CancellationToken);

        // Assert
        repository.Error.ToString().ShouldContain(message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("run --scenario smoke", 0)]
    [InlineData("run --scenario smoke --container --image " + K6TestRepository.Image + " --allow-external-target --allow-http-debug --allow-remote-output --root . -- --vus 2", 0)]
    [InlineData("run --scenaro smoke", 1)]
    public async Task Diagram_directive_reports_whether_a_command_line_parses_without_running(string commandLine, int expected)
    {
        // Arrange
        using var repository = new K6TestRepository();

        // Act
        var exitCode = await repository.Invoke(["[diagram]", .. CommandLineParser.SplitCommandLine(commandLine)], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(expected, "Consumer caller tests read the parse result from the exit code.");
        repository.RunDirectories().ShouldBe([]);
    }

    [Fact]
    public async Task Help_lists_the_run_command_and_its_options()
    {
        // Arrange
        using var repository = new K6TestRepository();

        // Act
        var exitCode = await repository.Invoke(["run", "--help"], TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        var help = repository.Output.ToString();
        foreach (var option in new[] { "--scenario", "--container", "--image", "--allow-external-target", "--allow-http-debug", "--allow-remote-output", "--root" })
        {
            help.ShouldContain(option, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_readme_command_line_parses_to_the_run_command()
    {
        // Arrange
        using var repository = new K6TestRepository();
        var root = repository.Create();
        var commandLines = DocumentedCommandLines.Find(DocumentedCommandLines.ReadToolReadme());

        // Act
        var failures = commandLines
            .Select(commandLine => (CommandLine: commandLine, Result: root.Parse(DocumentedCommandLines.Arguments(commandLine))))
            .Where(static entry => entry.Result.Errors.Count > 0
                || (entry.Result.Action is not HelpAction
                    && !ReferenceEquals(entry.Result.Action, entry.Result.CommandResult.Command.Action)))
            .Select(static entry => entry.CommandLine + " => " + string.Join("; ", entry.Result.Errors.Select(static error => error.Message)))
            .ToArray();

        // Assert
        commandLines.ShouldNotBeEmpty();
        failures.ShouldBe([]);
    }
}
