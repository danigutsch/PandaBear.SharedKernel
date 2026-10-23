
namespace PandaBear.Testing.CodeFixRunner.Tests;

public sealed class CodeFixRunnerToolTests
{
    [Fact]
    public void Project_is_packaged_as_dotnet_tool()
    {
        // Arrange
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        string? repositoryRoot = null;
        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "SharedKernel.slnx");
            if (File.Exists(solutionPath))
            {
                repositoryRoot = currentDirectory.FullName;
                break;
            }

            currentDirectory = currentDirectory.Parent;
        }

        var root = repositoryRoot.ShouldNotBeNull();
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
        properties["PackageId"].ShouldBe("PandaBear.SharedKernel.Testing.CodeFixRunner");
        properties["PackageReadmeFile"].ShouldBe("README.md");
        readmeItem.ShouldNotBeNull();
    }
}
