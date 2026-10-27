
namespace PandaBear.Testing.CodeFixRunner.Tests;

/// <summary>Tests the packaging metadata of the code fix runner project.</summary>
public sealed class CodeFixRunnerToolTests
{
    [Fact]
    public void Project_is_packaged_as_dotnet_tool()
    {
        // Arrange
        var projectPath = Path.Combine(
            RepositoryFiles.GetRootPath(),
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
