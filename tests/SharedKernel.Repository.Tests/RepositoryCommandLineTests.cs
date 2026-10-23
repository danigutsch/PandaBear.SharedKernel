namespace PandaBear.Repository.Tests;

public sealed class RepositoryCommandLineTests
{
    [Fact]
    public async Task Help_lists_the_select_validate_and_verify_pr_title_commands()
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
        result.Output.ShouldContain("  verify-pr-title ", StringComparison.Ordinal);
        result.Error.ShouldBe(string.Empty);
        processes.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("select", "--base <revision>", "--github-output <path>", "--root <path>", "--selection <directory>", "[default: TestResults/selection]")]
    [InlineData("validate", "--test-group <group>", "[default: all]", "--root <path>", "--selection <directory>")]
    [InlineData("verify-pr-title", "--event <path> (REQUIRED)")]
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
    [InlineData("")]
    [InlineData("publish")]
    [InlineData("validate --version 1.0.0")]
    [InlineData("validate --base main")]
    [InlineData("select --test-group ordinary")]
    [InlineData("select --base")]
    [InlineData("verify-pr-title")]
    [InlineData("verify-pr-title --event")]
    [InlineData("verify-pr-title --event event.json extra")]
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
    [InlineData("select", "--root")]
    [InlineData("select", "--selection")]
    [InlineData("select", "--github-output")]
    [InlineData("validate", "--root")]
    [InlineData("validate", "--selection")]
    [InlineData("verify-pr-title", "--event")]
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
