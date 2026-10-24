using System.Xml.Linq;
using PandaBear.Testing;

namespace PandaBear.Versioning.Tests;

[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class PackagedToolTests
{
    // Shipping the vault manager is a separate decision, so candidates do not pack it.
    private const string VaultManagerProject = "tools/SharedKernel.VaultManager.Tool/SharedKernel.VaultManager.Tool.csproj";

    [Fact]
    public static void Candidates_pack_every_dotnet_tool_project_except_the_vault_manager()
    {
        // Arrange
        var root = RepositoryRoot.Find();
        var toolProjects = Directory.GetFiles(Path.Combine(root, "tools"), "*.csproj", SearchOption.AllDirectories)
            .Where(static project => XDocument.Load(project).Descendants()
                .Any(static element => element.Name.LocalName == "PackAsTool" && element.Value.Trim() == "true"))
            .Select(project => Path.GetRelativePath(root, project).Replace('\\', '/'))
            .Where(static project => project != VaultManagerProject)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Act
        var packedProjects = SharedKernelPackagePlanner.FindProjects(root)
            .Select(project => Path.GetRelativePath(root, project).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);
        var unpackedTools = toolProjects.Where(project => !packedProjects.Contains(project)).ToArray();

        // Assert
        toolProjects.ShouldNotBeEmpty();
        unpackedTools.ShouldBe([], "SharedKernelPackagePlanner.PackagedTools must name every tool package, because FindProjects skips a name that has no project.");
    }
}
