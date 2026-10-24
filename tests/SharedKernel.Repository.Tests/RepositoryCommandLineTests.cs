namespace PandaBear.Repository.Tests;

public sealed class RepositoryCommandLineTests
{
    [Fact]
    public async Task Help_lists_the_select_validate_and_verify_commit_message_commands()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["--help"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("  select ", StringComparison.Ordinal);
        result.Output.ShouldContain("  validate ", StringComparison.Ordinal);
        result.Output.ShouldContain("  verify-commit-message ", StringComparison.Ordinal);
        result.Error.ShouldBe(string.Empty);
        processes.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("select", "--base <revision>", "--github-output <path>", "--root <path>", "--selection <directory>", "[default: TestResults/selection]")]
    [InlineData("validate", "--test-group <group>", "[default: all]", "--root <path>", "--selection <directory>")]
    [InlineData("verify-commit-message", "--event <path>", "--file <path>")]
    public async Task Command_help_lists_the_command_options(string command, params string[] expectedText)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(expectedText);
        using var scope = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, [command, "--help"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        foreach (var text in expectedText)
        {
            result.Output.ShouldContain(text, StringComparison.Ordinal);
        }

        result.Error.ShouldBe(string.Empty);
        processes.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Version_option_prints_the_tool_version()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["--version"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.Trim().ShouldNotBe(string.Empty);
        result.Error.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("[diagram] select --base main", 0)]
    [InlineData("[diagram] validate --test-group ordinary", 0)]
    [InlineData("[diagram] verify-commit-message --event event.json", 0)]
    [InlineData("[diagram] verify-commit-message --file COMMIT_EDITMSG", 0)]
    [InlineData("[diagram] verify-commit-message --event event.json --file COMMIT_EDITMSG", 1)]
    [InlineData("[diagram] select --bsae main", 1)]
    public async Task Diagram_directive_reports_whether_a_command_line_parses_without_running_it(string commandLine, int expectedExitCode)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(commandLine);
        var arguments = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        using var scope = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, arguments, processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(expectedExitCode, "Consumer caller tests read the parse result from the exit code.");
        result.Output.ShouldContain("[ " + arguments[1], StringComparison.Ordinal);
        processes.Calls.ShouldBeEmpty();
        Directory.Exists(Path.Combine(scope.Root, "TestResults")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("publish")]
    [InlineData("validate --version 1.0.0")]
    [InlineData("validate --base main")]
    [InlineData("select --test-group ordinary")]
    [InlineData("select --base")]
    [InlineData("verify-commit-message")]
    [InlineData("verify-commit-message --event")]
    [InlineData("verify-commit-message --file")]
    [InlineData("verify-commit-message --event event.json --file COMMIT_EDITMSG")]
    [InlineData("verify-commit-message --event event.json extra")]
    [InlineData("verify-commit-message --file COMMIT_EDITMSG extra")]
    public async Task Invalid_command_lines_fail_with_the_parse_error_exit_code_before_running_processes(string commandLine)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(commandLine);
        using var scope = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries), processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldNotBe(string.Empty);
        processes.Calls.ShouldBeEmpty();
        Directory.Exists(Path.Combine(scope.Root, "TestResults")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("verify-commit-message", "Option '--event' or '--file' is required.")]
    [InlineData("verify-commit-message --event event.json --file COMMIT_EDITMSG", "Options '--event' and '--file' cannot be combined.")]
    public async Task Verify_commit_message_requires_exactly_one_of_the_event_and_file_options(string commandLine, string expectedError)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(commandLine);
        using var scope = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries), processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1, "A missing or doubled message source is a parse error, so no file is read.");
        result.Error.ShouldContain(expectedError, StringComparison.Ordinal);
        processes.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("select", "--root")]
    [InlineData("select", "--selection")]
    [InlineData("select", "--github-output")]
    [InlineData("validate", "--root")]
    [InlineData("validate", "--selection")]
    [InlineData("verify-commit-message", "--event")]
    [InlineData("verify-commit-message", "--file")]
    public async Task Path_options_reject_empty_values_before_running_processes(string command, string option)
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, [command, option, ""], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain(option, StringComparison.Ordinal);
        processes.Calls.ShouldBeEmpty();
        Directory.Exists(Path.Combine(scope.Root, "TestResults")).ShouldBeFalse();
        File.Exists(Path.Combine(scope.Root, "revision.txt")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-p")]
    [InlineData("--output=changes.txt")]
    public async Task Select_rejects_a_base_that_git_would_not_read_as_a_revision(string revision)
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["select", "--base", revision], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("--base", StringComparison.Ordinal);
        processes.Calls.ShouldBeEmpty();
        Directory.Exists(Path.Combine(scope.Root, "TestResults")).ShouldBeFalse();
    }
}
