using System.Xml.Linq;
using PandaBear.Testing;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// Tests which projects the package planner packs as candidates.
/// </summary>
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
    [Fact]
    public static void Candidates_include_every_packable_consumer_test_support_project()
    {
        // Arrange
        var root = RepositoryRoot.Find();
        var supportProjects = Directory.GetFiles(Path.Combine(root, "tests"), "*.csproj", SearchOption.AllDirectories)
            .Where(static project => XDocument.Load(project).Descendants("PackageId")
                .Any(static element => element.Value.StartsWith("PandaBear.SharedKernel.", StringComparison.Ordinal)))
            .Where(static project => !XDocument.Load(project).Descendants("IsPackable")
                .Any(static element => element.Value.Trim() == "false"))
            .ToArray();

        // Act
        var packed = SharedKernelPackagePlanner.FindProjects(root).ToHashSet(StringComparer.Ordinal);
        var missing = supportProjects.Where(project => !packed.Contains(project)).ToArray();

        // Assert
        supportProjects.ShouldNotBeEmpty();
        missing.ShouldBeEmpty();
    }
}
