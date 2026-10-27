using System.CommandLine.Parsing;

namespace PandaBear.Testing.CodeFixRunner.Tests;

/// <summary>Tests that the command lines documented in the repository READMEs are accepted by the code fix runner command line.</summary>
public sealed class CodeFixRunnerCallerTests
{
    private const string ReadmePath = "tools/SharedKernel.Testing.CodeFixRunner/README.md";
    private const string TestingAnalyzerReadmePath = "src/SharedKernel/SharedKernel.Testing.Analyzers/README.md";
    private const string ProjectRun = "tools/SharedKernel.Testing.CodeFixRunner -c Release -- ";

    // The target must be an existing file, so the placeholder target is replaced with one. The synopsis is not a
    // command line.
    private const string TargetPlaceholder = "<project-or-solution>";
    private const string ProjectPlaceholder = "<project>";
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
        var projectRunCommandLines = RepositoryFiles.FindToolArguments(ReadmePath, ProjectRun);
        var localToolCommandLines = RepositoryFiles.FindToolArguments(ReadmePath, "dotnet tool run sharedkernel-codefix -- ");
        var parseErrors = commandLines
            .Concat(projectRunCommandLines)
            .Concat(localToolCommandLines)
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
            "tests/SharedKernel.Versioning.Tests/SharedKernel.Versioning.Tests.csproj",
            "SharedKernel.slnx --diagnostic SKTEST006",
        ]);
        projectRunCommandLines.ShouldBe([TargetPlaceholder]);
        localToolCommandLines.ShouldBe([TargetPlaceholder]);
        parseErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Testing_analyzer_readme_command_lines_parse_against_the_repository_root()
    {
        // Arrange
        var rootCommand = CodeFixRunnerCommandLine.Create(
            RepositoryFiles.GetRootPath(),
            static (_, _, _) => Task.FromResult<int?>(0));

        // Act
        var commandLines = RepositoryFiles.FindToolArguments(TestingAnalyzerReadmePath, ProjectRun);
        var parseErrors = commandLines
            .Select(static commandLine => commandLine.Replace(ProjectPlaceholder, ExistingTarget, StringComparison.Ordinal))
            .SelectMany(commandLine => rootCommand
                .Parse([.. CommandLineParser.SplitCommandLine(commandLine)])
                .Errors
                .Select(parseError => $"{commandLine}: {parseError.Message}"))
            .ToArray();

        // Assert
        commandLines.ShouldBe([ProjectPlaceholder + " --diagnostic SKTEST017"]);
        parseErrors.ShouldBeEmpty();
    }
}
