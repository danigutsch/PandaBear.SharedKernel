namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the <c>lockExemptProjects</c> policy of the <c>dependency snapshot</c> command.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class DependencySnapshotLockExemptionTests
{
    private const string Lock = """{"version":2,"dependencies":{"net10.0":{"Direct.Package":{"type":"Direct","resolved":"1.2.3","contentHash":"HASH"}}}}""";
    private const string TwoProjects = """<Solution><Project Path="src/Example.csproj" /><Project Path="lib/Exempt.csproj" /></Solution>""";
    private const string ExemptConfiguration = """{"version":1,"lockExemptProjects":["lib/Exempt.csproj"]}""";

    [Fact]
    public async Task Snapshot_needs_no_lock_for_a_listed_project_and_emits_no_manifest_for_it()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create(Lock, TwoProjects);
        repository.WriteFile("lib/Exempt.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        repository.WriteConfiguration(ExemptConfiguration);
        await repository.Commit();

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(0);
        using var snapshot = repository.ReadSnapshot();
        var manifests = snapshot.RootElement.GetProperty("manifests").EnumerateObject().Select(manifest => manifest.Name).ToArray();
        manifests.ShouldBe(["src/packages.lock.json#net10.0"]);
    }

    [Fact]
    public async Task Snapshot_still_requires_a_lock_for_a_project_that_is_not_listed()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create(Lock, TwoProjects);
        repository.WriteFile("lib/Exempt.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        repository.WriteConfiguration("""{"version":1,"lockExemptProjects":["src/Other.csproj"]}""");
        await repository.Commit();

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("dependency snapshot:", StringComparison.Ordinal);
        repository.SnapshotExists.ShouldBeFalse();
    }

    [Theory]
    [InlineData("""{"version":1}""")]
    [InlineData("""{"version":1,"lockExemptProjects":[]}""")]
    public async Task Snapshot_without_exemptions_requires_every_project_lock(string configuration)
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create(Lock, TwoProjects);
        repository.WriteFile("lib/Exempt.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        repository.WriteConfiguration(configuration);
        await repository.Commit();

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        repository.SnapshotExists.ShouldBeFalse();
    }

    [Fact]
    public async Task Snapshot_without_a_configuration_file_requires_every_project_lock()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create(Lock, TwoProjects);
        repository.WriteFile("lib/Exempt.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        await repository.Commit();

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        repository.SnapshotExists.ShouldBeFalse();
    }

    [Fact]
    public async Task Snapshot_rejects_a_listed_project_that_is_not_in_the_solution()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create(Lock);
        repository.WriteConfiguration("""{"version":1,"lockExemptProjects":["lib/Gone.csproj"]}""");
        await repository.Commit();

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("dependency snapshot:", StringComparison.Ordinal);
        repository.SnapshotExists.ShouldBeFalse();
    }

    [Fact]
    public async Task Snapshot_rejects_a_listed_project_that_also_has_a_committed_lock()
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create(Lock, TwoProjects);
        repository.WriteFile("lib/Exempt.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        repository.WriteFile("lib/packages.lock.json", Lock.Replace("HASH", Convert.ToBase64String(new byte[64]), StringComparison.Ordinal));
        repository.WriteConfiguration(ExemptConfiguration);
        await repository.Commit();

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        repository.SnapshotExists.ShouldBeFalse();
    }

    [Theory]
    [InlineData("""{"version":1,"lockExemptProjects":["/abs/Exempt.csproj"]}""")]
    [InlineData("""{"version":1,"lockExemptProjects":["../Exempt.csproj"]}""")]
    [InlineData("""{"version":1,"lockExemptProjects":["lib/Exempt.txt"]}""")]
    [InlineData("""{"version":1,"lockExemptProjects":["lib/Exempt.csproj","lib/Exempt.csproj"]}""")]
    [InlineData("""{"version":1,"lockExemptProjects":["lib\\Exempt.csproj"]}""")]
    [InlineData("""{"version":1,"lockExemptProjects":null}""")]
    [InlineData("""{"version":1,"lockExemptProjects":"lib/Exempt.csproj"}""")]
    public async Task Snapshot_rejects_an_invalid_exemption_list(string configuration)
    {
        // Arrange
        using var repository = await DependencySnapshotTestRepository.Create(Lock, TwoProjects);
        repository.WriteFile("lib/Exempt.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        repository.WriteConfiguration(configuration);
        await repository.Commit();

        // Act
        var result = await repository.Run();

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("dependency snapshot:", StringComparison.Ordinal);
        repository.SnapshotExists.ShouldBeFalse();
    }
}
