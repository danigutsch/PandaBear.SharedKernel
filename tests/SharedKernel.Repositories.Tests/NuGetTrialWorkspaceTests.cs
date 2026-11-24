namespace PandaBear.Repositories.Tests;

/// <summary>Verifies that a NuGet trial hands back only the requested central props and existing locks.</summary>
public sealed class NuGetTrialWorkspaceTests
{
    [Theory]
    [InlineData("src/Example.csproj", "outside the selected central props")]
    [InlineData("packages.lock.json", "created a new package lock")]
    public async Task Review_patch_rejects_writes_outside_the_planned_change(string changedPath, string diagnostic)
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        var ct = TestContext.Current.CancellationToken;
        await repository.Initialize(ct);
        await repository.Commit("src/Example.csproj", "<Project />", "Initial project", ct);
        var revision = await new RoadmapGitRepository().ReadHead(repository.RootPath, ct);
        using var workspace = new NuGetTrialWorkspace();
        await workspace.Initialize(repository.RootPath, revision, [], ct);
        await File.WriteAllTextAsync(Path.Combine(workspace.Root, changedPath), "candidate", ct);

        // Act
        Func<Task<string>> review = () => workspace.ReviewPatch("Directory.Packages.props", ct);

        // Assert
        var error = await review.ShouldThrow<InvalidOperationException>();
        error.Message.ShouldContain(diagnostic, StringComparison.Ordinal);
        var source = await File.ReadAllTextAsync(Path.Combine(repository.RootPath, "src/Example.csproj"), ct);
        source.ShouldBe("<Project />");
        File.Exists(Path.Combine(repository.RootPath, "packages.lock.json")).ShouldBeFalse();
    }

    [Theory]
    [InlineData(".")]
    [InlineData(".git/config")]
    public void Repository_path_rejects_the_checkout_and_git_metadata(string path)
    {
        // Arrange
        using var repository = new TemporaryDirectory();

        // Act
        Func<string> resolve = () => NuGetTrialWorkspace.RepositoryPath(repository.Path, path);

        // Assert
        resolve.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Repository_path_requires_a_relative_path_even_inside_the_checkout()
    {
        // Arrange
        using var repository = new TemporaryDirectory();
        var absolute = Path.Combine(repository.Path, "local", "packages");

        // Act
        Func<string> resolve = () => NuGetTrialWorkspace.RepositoryPath(repository.Path, absolute);

        // Assert
        resolve.ShouldThrow<ArgumentException>();
    }
}
