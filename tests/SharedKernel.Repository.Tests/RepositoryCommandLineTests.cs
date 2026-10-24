namespace PandaBear.Repository.Tests;

public sealed class RepositoryCommandLineTests
{
    [Fact]
    public async Task Help_lists_the_select_run_openapi_install_playwright_coverage_stage_candidate_and_verify_commit_message_commands()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["--help"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("  select ", StringComparison.Ordinal);
        result.Output.ShouldContain("  run ", StringComparison.Ordinal);
        result.Output.ShouldContain("  openapi ", StringComparison.Ordinal);
        result.Output.ShouldContain("  install-playwright ", StringComparison.Ordinal);
        result.Output.ShouldContain("  coverage ", StringComparison.Ordinal);
        result.Output.ShouldNotContain("  validate ", StringComparison.Ordinal);
        result.Output.ShouldContain("  stage-candidate ", StringComparison.Ordinal);
        result.Output.ShouldContain("  verify-commit-message ", StringComparison.Ordinal);
        result.Error.ShouldBe(string.Empty);
        processes.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("select", "--base <object-id>", "--github-output <path>", "--root <path>", "--selection <directory>", "[default: TestResults/selection]")]
    [InlineData(
        "run",
        "--slice <name>",
        "--configuration <name>",
        "[default: Release]",
        "--coverage",
        "--install-playwright",
        "--playwright-dependencies",
        "--github-summary <path>",
        "--root <path>",
        "--selection <directory>")]
    [InlineData("openapi", "generate", "refresh")]
    [InlineData("openapi generate", "--target <name>", "--root <path>")]
    [InlineData("openapi refresh", "--target <name>", "--root <path>")]
    [InlineData("coverage", "record-symbols", "verify")]
    [InlineData("coverage record-symbols", "--output <file>", "[default: TestResults/symbol-evidence.json]", "--root <path>")]
    [InlineData(
        "coverage verify",
        "--report <path> (REQUIRED)",
        "--base <object-id> (REQUIRED)",
        "--symbols <file> (REQUIRED)",
        "--output <file>",
        "[default: TestResults/coverage-gate.json]",
        "--root <path>")]
    [InlineData("install-playwright", "--project <path> (REQUIRED)", "--browser <name>", "--with-dependencies", "--configuration <name>", "--root <path>")]
    [InlineData(
        "stage-candidate",
        "--pin <file> (REQUIRED)",
        "--library-root <path> (REQUIRED)",
        "--output <directory> (REQUIRED)",
        "--repository <owner/name>",
        "--feed <directory>",
        "--root <path>")]
    [InlineData("verify-commit-message", "--event <path>", "--file <path>")]
    public async Task Command_help_lists_the_command_options(string command, params string[] expectedText)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(expectedText);
        using var scope = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, [.. command.Split(' '), "--help"], processes.Run, TestContext.Current.CancellationToken);

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
    [InlineData("[diagram] run --slice library", 0)]
    [InlineData("[diagram] run --slice library --slice hosted --coverage --install-playwright --playwright-dependencies --github-summary summary.md", 0)]
    [InlineData("[diagram] run --slice ../library", 1)]
    [InlineData("[diagram] openapi refresh --target admin --target catalog", 0)]
    [InlineData("[diagram] install-playwright --project tests/A/A.csproj --browser chromium --with-dependencies", 0)]
    [InlineData("[diagram] verify-commit-message --event event.json", 0)]
    [InlineData("[diagram] verify-commit-message --file COMMIT_EDITMSG", 0)]
    [InlineData("[diagram] verify-commit-message --event event.json --file COMMIT_EDITMSG", 1)]
    [InlineData("[diagram] stage-candidate --pin pin.json --library-root sk --output feed --feed packages", 0)]
    [InlineData("[diagram] stage-candidate --pin pin.json --library-root sk --output feed --repository danigutsch/PandaBear.SharedKernel", 0)]
    [InlineData("[diagram] select --bsae main", 1)]
    [InlineData("[diagram] stage-candidate --pin pin.json --library-root sk --output feed", 1)]
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
    [InlineData("validate --slice library")]
    [InlineData("run --version 1.0.0")]
    [InlineData("run --base main")]
    [InlineData("run --slice build")]
    [InlineData("run --slice Library")]
    [InlineData("run --slice library --slice library")]
    [InlineData("run --playwright-dependencies")]
    [InlineData("run --configuration -c")]
    [InlineData("run --configuration Release;Debug")]
    [InlineData("openapi")]
    [InlineData("openapi generate --target Admin")]
    [InlineData("openapi generate extra")]
    [InlineData("install-playwright")]
    [InlineData("install-playwright --project tests/A/A.csproj --browser Chromium")]
    [InlineData("select --test-group ordinary")]
    [InlineData("select --base")]
    [InlineData("verify-commit-message")]
    [InlineData("verify-commit-message --event")]
    [InlineData("verify-commit-message --file")]
    [InlineData("verify-commit-message --event event.json --file COMMIT_EDITMSG")]
    [InlineData("verify-commit-message --event event.json extra")]
    [InlineData("verify-commit-message --file COMMIT_EDITMSG extra")]
    [InlineData("stage-candidate --library-root sk --output feed --feed packages")]
    [InlineData("stage-candidate --pin pin.json --output feed --feed packages")]
    [InlineData("stage-candidate --pin pin.json --library-root sk --feed packages")]
    [InlineData("stage-candidate --pin pin.json --library-root sk --output feed --feed packages --base main")]
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
    [InlineData("run", "--root")]
    [InlineData("run", "--selection")]
    [InlineData("run", "--github-summary")]
    [InlineData("openapi generate", "--root")]
    [InlineData("install-playwright --project tests/A/A.csproj", "--root")]
    [InlineData("install-playwright", "--project")]
    [InlineData("verify-commit-message", "--event")]
    [InlineData("verify-commit-message", "--file")]
    public async Task Path_options_reject_empty_values_before_running_processes(string command, string option)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(command);
        using var scope = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, [.. command.Split(' '), option, ""], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain(option, StringComparison.Ordinal);
        processes.Calls.ShouldBeEmpty();
        Directory.Exists(Path.Combine(scope.Root, "TestResults")).ShouldBeFalse();
        File.Exists(Path.Combine(scope.Root, "revision.txt")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("", "Option '--repository' or '--feed' is required.")]
    [InlineData("--repository danigutsch/PandaBear.SharedKernel --feed packages", "Options '--repository' and '--feed' cannot be combined.")]
    public async Task Stage_candidate_requires_exactly_one_package_source(string sources, string expectedError)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(sources);
        using var scope = new RepositoryTestScope();
        string[] arguments = ["stage-candidate", "--pin", "pin.json", "--library-root", "sk", "--output", "feed", .. sources.Split(' ', StringSplitOptions.RemoveEmptyEntries)];

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, arguments, new ProcessLog().Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain(expectedError, StringComparison.Ordinal);
        Directory.Exists(Path.Combine(scope.Root, "feed")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("danigutsch")]
    [InlineData("danigutsch/")]
    [InlineData("/PandaBear.SharedKernel")]
    [InlineData("danigutsch/PandaBear/SharedKernel")]
    [InlineData("dani_gutsch/PandaBear.SharedKernel")]
    [InlineData("danigutsch/..")]
    [InlineData("danigutsch/.")]
    [InlineData("danigutsch/Panda Bear")]
    [InlineData("https://github.com/danigutsch/PandaBear.SharedKernel")]
    public async Task Stage_candidate_rejects_a_repository_that_is_not_owner_and_name(string repository)
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        string[] arguments = ["stage-candidate", "--pin", "pin.json", "--library-root", "sk", "--output", "feed", "--repository", repository];

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, arguments, new ProcessLog().Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("--repository requires owner/name", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--pin")]
    [InlineData("--library-root")]
    [InlineData("--output")]
    [InlineData("--feed")]
    [InlineData("--root")]
    public async Task Stage_candidate_path_options_reject_empty_values(string option)
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        string[] arguments = ["stage-candidate", "--pin", "pin.json", "--library-root", "sk", "--output", "feed", "--feed", "packages", "--root", "."];
        arguments[Array.IndexOf(arguments, option) + 1] = string.Empty;

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, arguments, new ProcessLog().Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain(option + " requires a path.", StringComparison.Ordinal);
        Directory.Exists(Path.Combine(scope.Root, "feed")).ShouldBeFalse();
    }
}
