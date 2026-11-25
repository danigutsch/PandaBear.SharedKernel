using System.Text.Json;

namespace SharedKernel.Repositories.Tests;

/// <summary>Verifies comparison of evaluated NuGet package graphs.</summary>
public sealed class NuGetRestoreGraphTests
{
    [Fact]
    public async Task Read_compares_changes_in_an_intentionally_lock_exempt_project()
    {
        // Arrange
        using var baselineWorkspace = new TemporaryRepoConfigWorkspace();
        using var candidateWorkspace = new TemporaryRepoConfigWorkspace();
        NuGetRestoreGraphTestAssets.WriteLockExemptProject(baselineWorkspace, NuGetRestoreGraphTestAssets.Unpinned);
        NuGetRestoreGraphTestAssets.WriteLockExemptProject(candidateWorkspace, NuGetRestoreGraphTestAssets.Upgraded);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var baseline = await NuGetRestoreGraphReader.Read(
            baselineWorkspace.RootPath, Path.Combine(baselineWorkspace.RootPath, "evaluated.json"), cancellationToken);
        var candidate = await NuGetRestoreGraphReader.Read(
            candidateWorkspace.RootPath, Path.Combine(candidateWorkspace.RootPath, "evaluated.json"), cancellationToken);

        // Assert
        var before = baseline["Provider.csproj"];
        var after = candidate["Provider.csproj"];
        before.LockExempt.ShouldBeTrue();
        after.LockExempt.ShouldBeTrue();
        before.Graph.ShouldNotBeNull();
        after.Graph.ShouldNotBeNull();
        var changes = before.Graph.Compare(after.Graph);
        changes.ShouldHaveCount(2);
    }

    [Theory]
    [InlineData(false, "Restore inputs are stale or the latest restore failed.")]
    [InlineData(true, "Restore assets or cache are missing.")]
    public async Task Read_reports_unavailable_restore_evidence(bool removeAssets, string expected)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        NuGetRestoreGraphTestAssets.WriteLockExemptProject(workspace, NuGetRestoreGraphTestAssets.Unpinned, stale: true);
        if (removeAssets)
        {
            workspace.DeleteFile("obj/project.assets.json");
        }

        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var projects = await NuGetRestoreGraphReader.Read(
            workspace.RootPath, Path.Combine(workspace.RootPath, "evaluated.json"), cancellationToken);

        // Assert
        var project = projects["Provider.csproj"];
        project.LockExempt.ShouldBeTrue();
        project.Graph.ShouldBeNull();
        project.Unavailable.ShouldBe(expected);
    }

    [Fact]
    public async Task Read_reports_a_malformed_restore_cache_as_unavailable()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        NuGetRestoreGraphTestAssets.WriteLockExemptProject(workspace, NuGetRestoreGraphTestAssets.Unpinned);
        await File.WriteAllTextAsync(Path.Combine(workspace.RootPath, "obj/project.nuget.cache"), "{", TestContext.Current.CancellationToken);

        // Act
        var projects = await NuGetRestoreGraphReader.Read(workspace.RootPath,
            Path.Combine(workspace.RootPath, "evaluated.json"), TestContext.Current.CancellationToken);

        // Assert
        var project = projects["Provider.csproj"];
        project.LockExempt.ShouldBeTrue();
        project.Graph.ShouldBeNull();
        project.Unavailable.ShouldBe("Restore evidence is malformed, outside the checkout, or unreadable.");
    }

    [Theory]
    [InlineData("\"version\": 4", "\"version\": 2", "Unsupported NuGet assets document version.")]
    [InlineData("\"Provider/2.0.0\"", "\"Provider\"", "NuGet package identity must contain a name and version.")]
    [InlineData("\"targets\": {", "\"targets\": {}, \"unused\": {", "The NuGet assets document contains no targets.")]
    public void Graph_rejects_an_unsupported_or_incomplete_assets_document(string oldValue, string newValue, string diagnostic)
    {
        // Arrange
        using var assets = JsonDocument.Parse(NuGetRestoreGraphTestAssets.Unpinned.Replace(oldValue, newValue, StringComparison.Ordinal));

        // Act
        Func<NuGetRestoreGraph> read = () => NuGetRestoreGraph.Read(assets.RootElement);

        // Assert
        var error = read.ShouldThrow<InvalidDataException>();
        error.Message.ShouldBe(diagnostic);
    }

    [Fact]
    public void Compare_reports_every_removed_package_when_a_project_disappears()
    {
        // Arrange
        using var assets = JsonDocument.Parse(NuGetRestoreGraphTestAssets.Unpinned);
        var baseline = NuGetRestoreGraph.Read(assets.RootElement);

        // Act
        var removed = baseline.Compare(NuGetRestoreGraph.Empty);

        // Assert
        removed.Select(change => change.Package).ShouldBe(["Provider", "Provider.Core"]);
        removed.ShouldAllSatisfy(change =>
        {
            change.Before.ShouldNotBeNull();
            change.After.ShouldBeNull();
        });
    }

    [Fact]
    public void Compare_reports_a_removed_transitive_pin_without_a_version_change()
    {
        // Arrange
        using var baseline = JsonDocument.Parse(NuGetRestoreGraphTestAssets.Pinned);
        using var candidate = JsonDocument.Parse(NuGetRestoreGraphTestAssets.Unpinned);

        // Act
        var changes = NuGetRestoreGraph.Read(baseline.RootElement)
            .Compare(NuGetRestoreGraph.Read(candidate.RootElement));

        // Assert
        var change = changes.ShouldHaveSingleItem();
        change.Target.ShouldBe("net11.0");
        change.Package.ShouldBe("Provider.Core");
        change.Before.ShouldNotBeNull();
        change.After.ShouldNotBeNull();
        change.Before.Version.ShouldBe("2.0.0");
        change.After.Version.ShouldBe("2.0.0");
        change.Before.Classification.ShouldBe(NuGetRestoreClassification.CentralTransitive);
        change.After.Classification.ShouldBe(NuGetRestoreClassification.Transitive);
    }

    [Fact]
    public void Compare_keeps_runtime_targets_separate_and_reports_package_changes()
    {
        // Arrange
        using var baseline = JsonDocument.Parse(NuGetRestoreGraphTestAssets.Unpinned);
        using var candidate = JsonDocument.Parse(NuGetRestoreGraphTestAssets.Upgraded);

        // Act
        var changes = NuGetRestoreGraph.Read(baseline.RootElement)
            .Compare(NuGetRestoreGraph.Read(candidate.RootElement));

        // Assert
        changes.ShouldHaveCount(2);
        var parent = changes[0];
        parent.Target.ShouldBe("net11.0");
        parent.Package.ShouldBe("Provider");
        parent.Before.ShouldNotBeNull();
        parent.After.ShouldNotBeNull();
        parent.After.Classification.ShouldBe(NuGetRestoreClassification.Direct);
        parent.After.Version.ShouldBe("3.0.0");
        parent.After.ContentHash.ShouldBe("provider-hash-v3");
        var dependency = parent.After.Dependencies.ShouldHaveSingleItem();
        dependency.ShouldBe(new KeyValuePair<string, string>("Provider.Core", "3.0.0"));
        var runtime = changes[1];
        runtime.Target.ShouldBe("net11.0/linux-x64");
        runtime.Package.ShouldBe("Provider.Core");
        runtime.Before.ShouldBeNull();
        runtime.After.ShouldNotBeNull();
        var asset = runtime.After.Assets.ShouldHaveSingleItem();
        asset.ShouldBe(new KeyValuePair<string, string>("native", "runtimes/linux-x64/native/libprovider.so"));
    }

    [Fact]
    public void Find_package_preserves_its_resolved_versions_across_framework_and_runtime_targets()
    {
        // Arrange
        using var assets = JsonDocument.Parse(NuGetRestoreGraphTestAssets.Upgraded);
        var graph = NuGetRestoreGraph.Read(assets.RootElement);

        // Act
        var selected = graph.FindPackage("provider.core").ToArray();
        var missing = graph.FindPackage("missing").ToArray();

        // Assert
        selected.Select(item => item.Target).ShouldBe(["net11.0", "net11.0/linux-x64"]);
        selected.Select(item => item.Package.Version).ShouldBe(["2.0.0", "3.0.0"]);
        selected.Select(item => item.Package.Classification)
            .ShouldBe([NuGetRestoreClassification.Transitive, NuGetRestoreClassification.Transitive]);
        missing.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Provider.Core", "2.0.0", true)]
    [InlineData("Provider.Core", "1.0.0", false)]
    [InlineData("Provider", "2.0.0", false)]
    public void Direct_owner_search_matches_the_affected_transitive_version(string package, string version, bool hasOwner)
    {
        // Arrange
        using var document = JsonDocument.Parse(NuGetRestoreGraphTestAssets.Pinned);
        var graph = NuGetRestoreGraph.Read(document.RootElement);
        string[] expected = hasOwner ? ["Provider"] : [];

        // Act
        var owners = graph.FindDirectOwners(package, version).ToArray();

        // Assert
        owners.ShouldBe(expected);
    }
}
