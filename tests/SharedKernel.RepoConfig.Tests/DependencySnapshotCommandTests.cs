namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the <c>dependency snapshot</c> command.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class DependencySnapshotCommandTests
{
    [Fact]
    public async Task Snapshot_preserves_every_framework_package_kind_and_project_edge_without_restore()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create("""
            {"version":2,"dependencies":{
              "net10.0":{
                "Direct.Package":{"type":"Direct","resolved":"1.2.3","contentHash":"HASH","dependencies":{"Leaf.Package":"[3.0.0, )","Referenced.Project":"1.0.0"}},
                "Central.Package":{"type":"CentralTransitive","resolved":"2.0.0","contentHash":"HASH","dependencies":{"Leaf.Package":"3.0.0"}},
                "Leaf.Package":{"type":"Transitive","resolved":"3.0.0","contentHash":"HASH"},
                "Unreachable.Package":{"type":"Transitive","resolved":"9.0.0","contentHash":"HASH"},
                "Referenced.Project":{"type":"Project","dependencies":{"Central.Package":"2.0.0"}}
              },
              ".NETStandard,Version=v2.0":{
                "Direct.Package":{"type":"Direct","resolved":"1.0.0","contentHash":"HASH"}
              }
            }}
            """);

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(0);
        using var snapshot = repository.ReadSnapshot();
        var root = snapshot.RootElement;
        root.GetProperty("sha").GetString().ShouldBe(repository.Head);
        root.GetProperty("ref").GetString().ShouldBe("refs/heads/test");
        root.GetProperty("job").GetProperty("id").GetString().ShouldBe("123");
        var manifests = root.GetProperty("manifests");
        manifests.EnumerateObject().Count().ShouldBe(2);
        var framework = manifests.GetProperty("src/packages.lock.json#net10.0");
        var nodes = framework.GetProperty("resolved");
        nodes.EnumerateObject().Count().ShouldBe(4);
        nodes.GetProperty("pkg:nuget/Direct.Package@1.2.3").GetProperty("relationship").GetString().ShouldBe("direct");
        nodes.GetProperty("pkg:nuget/Central.Package@2.0.0").GetProperty("relationship").GetString().ShouldBe("indirect");
        nodes.GetProperty("pkg:nuget/Unreachable.Package@9.0.0").GetProperty("package_url").GetString().ShouldBe("pkg:nuget/Unreachable.Package@9.0.0");
        nodes.GetProperty("pkg:nuget/Direct.Package@1.2.3").GetProperty("dependencies")[0].GetString().ShouldBe("pkg:nuget/Central.Package@2.0.0");
        nodes.GetProperty("pkg:nuget/Direct.Package@1.2.3").GetProperty("dependencies")[1].GetString().ShouldBe("pkg:nuget/Leaf.Package@3.0.0");
        nodes.TryGetProperty("project:Referenced.Project", out _).ShouldBeFalse();
        var everyNodeHasPackageUrl = nodes.EnumerateObject().All(node => node.Value.GetProperty("package_url").GetString() is not null);
        everyNodeHasPackageUrl.ShouldBeTrue();
        manifests.GetProperty("src/packages.lock.json#.NETStandard,Version=v2.0").GetProperty("resolved").GetProperty("pkg:nuget/Direct.Package@1.0.0").GetProperty("package_url").GetString().ShouldBe("pkg:nuget/Direct.Package@1.0.0");
        Directory.Exists(Path.Combine(repository.RootPath, "src", "obj")).ShouldBeFalse();
    }

    [Fact]
    public async Task Snapshot_rejects_amplified_package_urls_before_publishing_output()
    {
        // Arrange
        // A small committed lock expands beyond 32 MiB through repeated dependency URLs.
        var graph = new System.Text.StringBuilder("{\"version\":2,\"dependencies\":{\"net10.0\":{");
        graph.Append("\"Long.Package\":{\"type\":\"Transitive\",\"resolved\":\"");
        graph.Append('x', 256 * 1024);
        graph.Append("\",\"contentHash\":\"HASH\"}");
        for (var index = 0; index < 128; index++)
        {
            graph.Append(System.Globalization.CultureInfo.InvariantCulture,
                $",\"Package{index}\":{{\"type\":\"Direct\",\"resolved\":\"1.0.0\",\"contentHash\":\"HASH\",\"dependencies\":{{\"Long.Package\":\"1.0.0\"}}}}");
        }
        graph.Append("}}}");
        using var repository = await DependencySnapshotTestRepository.Create(graph.ToString());

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("dependency snapshot:", StringComparison.Ordinal);
        repository.SnapshotExists.ShouldBeFalse();
    }

    [Theory]
    [InlineData("{\"version\":99,\"dependencies\":{}}")]
    [InlineData("{\"version\":3,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Direct\",\"resolved\":\"1.0.0\",\"contentHash\":\"HASH\"}}}}")]
    [InlineData("{\"version\":2,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Unknown\",\"resolved\":\"1.0.0\",\"contentHash\":\"HASH\"}}}}")]
    [InlineData("{\"version\":2,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Direct\",\"contentHash\":\"HASH\"}}}}")]
    [InlineData("{\"version\":2,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Direct\",\"resolved\":\"1.0.0\",\"contentHash\":\"HASH\",\"dependencies\":{\"Absent\":\"1.0.0\"}}}}}")]
    [InlineData("{\"version\":2,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Project\"},\"a\":{\"type\":\"Project\"}}}}")]
    public async Task Snapshot_rejects_incomplete_or_ambiguous_lock_graphs(string graph)
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create(graph);

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("dependency snapshot:", StringComparison.Ordinal);
        repository.SnapshotExists.ShouldBeFalse();
    }

    [Fact]
    public async Task Snapshot_rejects_a_missing_project_lock_even_when_another_lock_is_valid()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create(
            "{\"version\":2,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Project\"}}}}",
            "<Solution><Project Path=\"src/Example.csproj\" /><Project Path=\"other/Missing.csproj\" /></Solution>");
        Directory.CreateDirectory(Path.Combine(repository.RootPath, "other"));
        await File.WriteAllTextAsync(Path.Combine(repository.RootPath, "other/Missing.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />", TestContext.Current.CancellationToken);
        await repository.Commit();

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("dependency snapshot:", StringComparison.Ordinal);
        repository.SnapshotExists.ShouldBeFalse();
    }

    [Fact]
    public async Task Snapshot_rejects_dirty_inputs()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create("{\"version\":2,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Project\"}}}}");
        repository.WriteLock("{}");

        // Act
        var dirty = await repository.Run();

        // Assert
        dirty.ExitCode.ShouldBe(1);
        repository.SnapshotExists.ShouldBeFalse();
    }


    [Fact]
    public async Task Snapshot_rejects_mislabeling_a_clean_commit()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create("{\"version\":2,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Project\"}}}}");

        // Act
        var result = await repository.Run(sha: new string('a', 40));

        // Assert
        result.ExitCode.ShouldBe(1);
        repository.SnapshotExists.ShouldBeFalse();
    }


    [Fact]
    public async Task Snapshot_rejects_an_ignored_lock_file_missing_from_the_commit()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create(null);
        await File.AppendAllTextAsync(Path.Combine(repository.RootPath, ".git/info/exclude"), "src/packages.lock.json\n", TestContext.Current.CancellationToken);
        repository.WriteLock("{\"version\":2,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Direct\",\"resolved\":\"1.0.0\",\"contentHash\":\"HASH\"}}}}");

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        repository.SnapshotExists.ShouldBeFalse();
    }

    [Fact]
    public async Task Snapshot_uses_committed_blobs_when_tracked_lock_changes_are_assumed_unchanged()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create("{\"version\":2,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Direct\",\"resolved\":\"1.0.0\",\"contentHash\":\"HASH\"}}}}");
        var index = await TestProcess.Run("git", repository.RootPath, ["update-index", "--assume-unchanged", "src/packages.lock.json"], TestContext.Current.CancellationToken);
        index.ExitCode.ShouldBe(0);
        repository.WriteLock("{\"version\":2,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Direct\",\"resolved\":\"9.9.9\",\"contentHash\":\"HASH\"}}}}");

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(0);
        using var snapshot = repository.ReadSnapshot();
        var nodes = snapshot.RootElement.GetProperty("manifests").GetProperty("src/packages.lock.json#net10.0").GetProperty("resolved");
        nodes.TryGetProperty("pkg:nuget/A@1.0.0", out _).ShouldBeTrue();
        nodes.TryGetProperty("pkg:nuget/A@9.9.9", out _).ShouldBeFalse();
    }


    [Fact]
    public async Task Snapshot_flattens_reconverging_project_connectors_without_duplicate_package_edges()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create("""
            {"version":2,"dependencies":{"net10.0":{
              "Root":{"type":"Direct","resolved":"1.0.0","contentHash":"HASH","dependencies":{"First":"1.0.0","Second":"1.0.0"}},
              "First":{"type":"Project","dependencies":{"Tail":"1.0.0"}},
              "Second":{"type":"Project","dependencies":{"Tail":"1.0.0"}},
              "Tail":{"type":"Project","dependencies":{"Leaf":"2.0.0"}},
              "Leaf":{"type":"Transitive","resolved":"2.0.0","contentHash":"HASH"}
            }}}
            """);

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(0);
        using var snapshot = repository.ReadSnapshot();
        var nodes = snapshot.RootElement.GetProperty("manifests").GetProperty("src/packages.lock.json#net10.0").GetProperty("resolved");
        nodes.EnumerateObject().Count().ShouldBe(2);
        var targets = nodes.GetProperty("pkg:nuget/Root@1.0.0").GetProperty("dependencies");
        targets.GetArrayLength().ShouldBe(1);
        targets[0].GetString().ShouldBe("pkg:nuget/Leaf@2.0.0");
    }

    [Fact]
    public async Task Snapshot_rejects_cyclic_local_project_connectors()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create("""
            {"version":2,"dependencies":{"net10.0":{
              "Root":{"type":"Direct","resolved":"1.0.0","contentHash":"HASH","dependencies":{"First":"1.0.0"}},
              "First":{"type":"Project","dependencies":{"Second":"1.0.0"}},
              "Second":{"type":"Project","dependencies":{"First":"1.0.0"}}
            }}}
            """);

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        repository.SnapshotExists.ShouldBeFalse();
    }


    [Fact]
    public async Task Snapshot_ignores_replacement_objects_for_a_reviewed_commit()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create("{\"version\":2,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Direct\",\"resolved\":\"1.0.0\",\"contentHash\":\"HASH\"}}}}");
        var original = await TestProcess.Run("git", repository.RootPath, ["rev-parse", "HEAD:src/packages.lock.json"], TestContext.Current.CancellationToken);
        original.ExitCode.ShouldBe(0);
        var content = await File.ReadAllTextAsync(Path.Combine(repository.RootPath, "src/packages.lock.json"), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(repository.RootPath, ".git/forged-lock.json"), content.Replace("1.0.0", "9.9.9", StringComparison.Ordinal), TestContext.Current.CancellationToken);
        var forged = await TestProcess.Run("git", repository.RootPath, ["hash-object", "--no-filters", "-w", ".git/forged-lock.json"], TestContext.Current.CancellationToken);
        forged.ExitCode.ShouldBe(0);
        var replaced = await TestProcess.Run("git", repository.RootPath, ["replace", original.StandardOutput.Trim(), forged.StandardOutput.Trim()], TestContext.Current.CancellationToken);
        replaced.ExitCode.ShouldBe(0);

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(0);
        using var snapshot = repository.ReadSnapshot();
        var nodes = snapshot.RootElement.GetProperty("manifests").GetProperty("src/packages.lock.json#net10.0").GetProperty("resolved");
        nodes.TryGetProperty("pkg:nuget/A@1.0.0", out _).ShouldBeTrue();
        nodes.TryGetProperty("pkg:nuget/A@9.9.9", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Snapshot_rejects_output_outside_the_reviewed_repository()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create("{\"version\":2,\"dependencies\":{\"net10.0\":{\"A\":{\"type\":\"Project\"}}}}");

        // Act
        var result = await repository.Run(destination: "../snapshot.json");

        // Assert
        result.ExitCode.ShouldBe(1);
        repository.SnapshotExists.ShouldBeFalse();
    }
}
