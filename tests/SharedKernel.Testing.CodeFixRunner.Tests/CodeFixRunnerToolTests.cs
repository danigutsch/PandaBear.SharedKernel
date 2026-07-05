
namespace SharedKernel.Testing.CodeFixRunner.Tests;

public sealed class CodeFixRunnerToolTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("--diagnostic", "SKTEST006", "--help", "sample.csproj")]
    [InlineData("sample.csproj", "-h")]
    public async Task Run_prints_help_when_help_option_is_present(params string[] args)
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await ProgramEntryPoint.Run(args, output, error);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("Usage:", StringComparison.Ordinal);
        error.ToString().ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("sample.csproj", "--version")]
    public async Task Run_prints_version_when_version_option_is_present(params string[] args)
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await ProgramEntryPoint.Run(args, output, error);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldNotBe(string.Empty);
        error.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void Project_is_packaged_as_dotnet_tool()
    {
        // Arrange
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        string? repositoryRoot = null;
        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "ViajantesTurismo.slnx");
            if (File.Exists(solutionPath))
            {
                repositoryRoot = currentDirectory.FullName;
                break;
            }

            currentDirectory = currentDirectory.Parent;
        }

        var root = (repositoryRoot).ShouldNotBeNull();
        var projectPath = Path.Combine(
            root,
            "tools",
            "SharedKernel.Testing.CodeFixRunner",
            "SharedKernel.Testing.CodeFixRunner.csproj");

        // Act
        var project = System.Xml.Linq.XDocument.Load(projectPath);
        var properties = project.Descendants().Where(element => element.Parent?.Name.LocalName == "PropertyGroup").ToDictionary(
            element => element.Name.LocalName,
            element => element.Value);
        var readmeItem = project.Descendants("None").SingleOrDefault(element =>
            string.Equals((string?)element.Attribute("Include"), "README.md", StringComparison.Ordinal));

        // Assert
        properties["PackAsTool"].ShouldBe("true");
        properties["ToolCommandName"].ShouldBe("sharedkernel-codefix");
        properties["PackageId"].ShouldBe("SharedKernel.Testing.CodeFixRunner");
        properties["PackageReadmeFile"].ShouldBe("README.md");
        (readmeItem).ShouldNotBeNull();
    }

    [Fact]
    public void Parse_uses_default_diagnostic_for_single_target_argument()
    {
        var options = CodeFixRunnerOptions.Parse(["sample.csproj"]);

        var parsedOptions = (options).ShouldNotBeNull();
        (parsedOptions.TargetPath).ShouldBe(Path.GetFullPath("sample.csproj"));
        (parsedOptions.DiagnosticId).ShouldBe("SKTEST004");
    }

    [Fact]
    public void Parse_uses_requested_diagnostic_when_option_is_provided()
    {
        var options = CodeFixRunnerOptions.Parse(["--diagnostic", "SKTEST006", "sample.csproj"]);

        var parsedOptions = (options).ShouldNotBeNull();
        (parsedOptions.TargetPath).ShouldBe(Path.GetFullPath("sample.csproj"));
        (parsedOptions.DiagnosticId).ShouldBe("SKTEST006");
    }

    [Theory]
    [InlineData()]
    [InlineData("--diagnostic")]
    [InlineData("--unknown", "SKTEST006", "sample.csproj")]
    [InlineData("sample.csproj", "extra")]
    public void Parse_returns_null_for_invalid_arguments(params string[] args)
    {
        var options = CodeFixRunnerOptions.Parse(args);

        (options).ShouldBeNull();
    }
}
