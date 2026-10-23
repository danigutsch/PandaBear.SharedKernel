using System.CommandLine.Parsing;

namespace PandaBear.Testing.CodeFixRunner.Tests;

public sealed class CodeFixRunnerCallerTests
{
    private const string ReadmePath = "tools/SharedKernel.Testing.CodeFixRunner/README.md";
    private const string Synopsis = "<project-or-solution> [--diagnostic <id>]";

    [Fact]
    public void Readme_command_lines_parse_against_the_repository_root()
    {
        // Arrange
        var rootCommand = CodeFixRunnerCommandLine.Create(
            RepositoryFiles.GetRootPath(),
            static (_, _, _) => Task.FromResult<int?>(0));

        // Act
        var commandLines = RepositoryFiles.FindToolArguments(ReadmePath, "sharedkernel-codefix ");
        var parseErrors = commandLines
            .Where(static commandLine => !string.Equals(commandLine, Synopsis, StringComparison.Ordinal))
            .SelectMany(commandLine => rootCommand
                .Parse([.. CommandLineParser.SplitCommandLine(commandLine)])
                .Errors
                .Select(parseError => $"{commandLine}: {parseError.Message}"))
            .ToArray();

        // Assert
        commandLines.ShouldBe(
        [
            Synopsis,
            "--help",
            "--version",
            "--help",
            "--help",
            "tests/SharedKernel.Versioning.Tests/SharedKernel.Versioning.Tests.csproj",
            "SharedKernel.slnx --diagnostic SKTEST006",
        ]);
        parseErrors.ShouldBeEmpty();
    }
}
