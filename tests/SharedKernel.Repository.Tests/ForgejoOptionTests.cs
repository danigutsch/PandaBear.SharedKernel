namespace PandaBear.Repository.Tests;

/// <summary>Guards the <c>--forgejo</c> option that points <c>prune-prereleases</c> and <c>stage-candidate</c> at a Forgejo instance.</summary>
public sealed class ForgejoOptionTests
{
    [Theory]
    [InlineData("http://forge.example")]
    [InlineData("forge.example")]
    [InlineData("https://user:secret@forge.example")]
    public async Task A_forgejo_address_that_is_not_plain_and_secure_fails_to_parse(string server)
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "1", "--forgejo", server]);

        // Assert
        result.ExitCode.ShouldNotBe(0);
        result.Error.ShouldContain("--forgejo requires an absolute HTTPS address", StringComparison.Ordinal);
        api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Forced_pruning_on_forgejo_without_its_token_sends_no_request()
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "1", "--force", "--forgejo", "https://forge.example"]);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(
            "Set FORGEJO_TOKEN to a token that may delete the releases of " + FakeGitHubReleasesApi.Repository + "; --force sent no request.", StringComparison.Ordinal);
        api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Pruning_on_forgejo_lists_through_the_forgejo_API()
    {
        // Arrange
        using var api = new FakeGitHubReleasesApi();

        // Act
        var result = await PrereleasePruningRuns.Run(api, ["--keep", "1", "--forgejo", "https://forge.example"]);

        // Assert
        result.ExitCode.ShouldBe(2);
        api.Requests.ShouldHaveSingleItem().Uri.ToString().ShouldBe(
            "https://forge.example/api/v1/repos/" + FakeGitHubReleasesApi.Repository + "/releases?limit=50&page=1");
    }

    [Fact]
    public async Task Staging_from_forgejo_requires_a_repository()
    {
        // Arrange
        using var scope = new CandidateStagingScope();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Base, [.. scope.Arguments(), "--forgejo", "https://forge.example"], new ProcessLog().Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldNotBe(0);
        result.Error.ShouldContain("Option '--forgejo' requires '--repository'.", StringComparison.Ordinal);
    }
}
