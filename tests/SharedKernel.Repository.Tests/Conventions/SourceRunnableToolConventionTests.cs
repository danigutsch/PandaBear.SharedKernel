using System.Xml.Linq;

namespace PandaBear.Repository.Tests.Conventions;

/// <summary>
/// Guards the tools that consumers build from a sparse checkout of <c>tools/&lt;tool&gt;</c> and <c>tools/Shared</c>
/// before a package candidate exists.
/// </summary>
public sealed class SourceRunnableToolConventionTests
{
    [Theory]
    [InlineData("SharedKernel.CI.Tool")]
    [InlineData("SharedKernel.RepoConfig.Tool")]
    public void Source_runnable_tools_build_from_their_directory_and_the_shared_tool_sources(string tool)
    {
        // Arrange
        var root = SharedKernelPackagingConventionTestFiles.GetRepositoryRoot();
        var projectDirectory = Path.Combine(root, "tools", tool);
        var sharedDirectory = Path.Combine(root, "tools", "Shared") + Path.DirectorySeparatorChar;
        var project = XDocument.Load(Path.Combine(projectDirectory, tool + ".csproj"));

        // Act
        var packageReferences = project.Descendants()
            .Where(static element => element.Name.LocalName == "PackageReference")
            .Select(static element => (string?)element.Attribute("Include") ?? string.Empty)
            .ToArray();
        var projectReferences = project.Descendants()
            .Where(static element => element.Name.LocalName == "ProjectReference")
            .Select(static element => (string?)element.Attribute("Include") ?? string.Empty)
            .ToArray();
        var linkedSources = project.Descendants()
            .Where(static element => element.Name.LocalName == "Compile" && element.Attribute("Link") is not null)
            .Select(static element => ((string?)element.Attribute("Include") ?? string.Empty).Replace('\\', '/'))
            .ToArray();

        // Assert
        packageReferences.ShouldBe(["System.CommandLine"], "The sparse checkout restores only the packages in the tool's own lock file.");
        projectReferences.ShouldBe([], "The sparse checkout contains no other project.");
        linkedSources.ShouldNotContain(
            include => !Path.GetFullPath(Path.Combine(projectDirectory, include)).StartsWith(sharedDirectory, StringComparison.Ordinal),
            "The sparse checkout contains only tools/Shared besides the tool's own directory.");
    }
}
