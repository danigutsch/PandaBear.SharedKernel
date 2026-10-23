using System.CommandLine.Parsing;

namespace PandaBear.Testing.CodeFixRunner.Tests;

public sealed class CodeFixRunnerCallerTests
{
    private const string ReadmePath = "tools/SharedKernel.Testing.CodeFixRunner/README.md";

    // The target must be an existing file, so the placeholder target is replaced with one. The synopsis is not a
    // command line.
    private const string TargetPlaceholder = "<project-or-solution>";
    private const string ExistingTarget = "SharedKernel.slnx";
    private const string Synopsis = TargetPlaceholder + " [--diagnostic <id>]";

    [Fact]
    public void Readme_command_lines_parse_against_the_repository_root()
    {
        // Arrange
        var rootCommand = CodeFixRunnerCommandLine.Create(
            RepositoryFiles.GetRootPath(),
            static (_, _, _) => Task.FromResult<int?>(0));

        // Act
        var commandLines = RepositoryFiles.FindToolArguments(ReadmePath, "sharedkernel-codefix ");
        var projectRunCommandLines = RepositoryFiles.FindToolArguments(ReadmePath, "tools/SharedKernel.Testing.CodeFixRunner -c Release -- ");
        var parseErrors = commandLines
            .Concat(projectRunCommandLines)
            .Where(static commandLine => commandLine != Synopsis)
            .Select(static commandLine => commandLine.Replace(TargetPlaceholder, ExistingTarget, StringComparison.Ordinal))
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
            TargetPlaceholder,
            "tests/SharedKernel.Versioning.Tests/SharedKernel.Versioning.Tests.csproj",
            "SharedKernel.slnx --diagnostic SKTEST006",
        ]);
        projectRunCommandLines.ShouldBe([TargetPlaceholder]);
        parseErrors.ShouldBeEmpty();
    }
}
