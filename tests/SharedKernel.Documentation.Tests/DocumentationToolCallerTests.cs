namespace PandaBear.Documentation.Tests;

/// <summary>Tests that the documentation tool invocations written in the CI workflow and READMEs still parse against the current command line.</summary>
[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.CommandLineCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class DocumentationToolCallerTests
{
    [Theory]
    [InlineData(
        ".github/workflows/ci.yml",
        "tools/SharedKernel.Documentation.Tool -c Release --no-build --no-restore -- ",
        "verify --config docs/architecture/generated-diagrams.json")]
    [InlineData(
        "tools/SharedKernel.Documentation.Tool/README.md",
        "sharedkernel-docs ",
        "generate --config <path>",
        "verify --config <path>",
        "check --config <path>")]
    [InlineData(
        "tools/SharedKernel.Documentation.Tool/README.md",
        "dotnet tool run sharedkernel-docs -- ",
        "verify --config <path>")]
    [InlineData(
        "tools/SharedKernel.Documentation.Tool/README.md",
        "tools/SharedKernel.Documentation.Tool -c Release -- ",
        "verify --config docs/architecture/generated-diagrams.json")]
    [InlineData(
        "src/SharedKernel/SharedKernel.Documentation/README.md",
        "sharedkernel-docs ",
        "check --config <path>")]
    public void Repository_callers_invoke_command_lines_that_parse(string callerPath, string invocation, params string[] expectedArguments)
    {
        // Arrange
        var rootCommand = DocumentationCommandLine.Create(RepositoryFiles.GetRootPath());

        // Act
        var callerArguments = RepositoryFiles.FindToolArguments(callerPath, invocation);

        // Assert
        callerArguments.ShouldBe(expectedArguments);
        callerArguments.ShouldAllSatisfy(arguments =>
        {
            var parseResult = rootCommand.Parse(arguments);
            parseResult.Errors.ShouldBeEmpty();
            parseResult.CommandResult.Command.Name.ShouldBe(arguments.Split(' ')[0]);
        });
    }
}
