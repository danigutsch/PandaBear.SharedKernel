using System.Text.Json;
using System.Text.Json.Nodes;
using static PandaBear.RepoConfig.Tests.RoadmapPublishResultTestOperations;

namespace PandaBear.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class RoadmapPublishResultCommandTests
{
    [Fact]
    public async Task Publish_sends_the_configured_pull_request_body_and_summarizes_the_url()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var publication = await CreatePublication(workspace, workspace => RoadmapConfigTestOperations.SetProperty(
            workspace, "automation", new JsonObject { ["pullRequestBody"] = "Refs #42" }));
        using var handler = publication.Handler;

        // Act
        var result = await Run(workspace, publication, force: true);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBe(string.Empty);
        var pullRequest = handler.Requests.ShouldHaveSingleItem(request => request.PathAndQuery == "/repos/owner/repository/pulls");
        using var body = JsonDocument.Parse(pullRequest.Body.ShouldNotBeNull());
        body.RootElement.GetProperty("body").GetString().ShouldBe("Refs #42");
        result.Summary.ShouldContain(RoadmapGitHubPublicationTestOperations.PullRequestUrl, StringComparison.Ordinal);
        result.Outputs.ShouldContain($"pull_request_url={RoadmapGitHubPublicationTestOperations.PullRequestUrl}", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_requires_the_configured_pull_request_body(bool force)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var publication = await CreatePublication(workspace, workspace => RoadmapConfigTestOperations.SetProperty(workspace, "automation", null));
        using var handler = publication.Handler;

        // Act
        var result = await Run(workspace, publication, force);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe("sharedkernel-repo: roadmap/config.json must define automation.pullRequestBody before roadmap publication." + Environment.NewLine);
        handler.Requests.ShouldAllSatisfy(request => request.PathAndQuery.ShouldBe("/graphql"));
    }

    [Theory]
    [InlineData("GITHUB_OUTPUT")]
    [InlineData("GITHUB_STEP_SUMMARY")]
    public async Task Publish_requires_the_workflow_files_before_writing_to_github(string missingVariable)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var publication = await CreatePublication(workspace);
        using var handler = publication.Handler;

        // Act
        var result = await Run(workspace, publication, force: true, missingVariable);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldBe($"sharedkernel-repo: Roadmap automation requires {missingVariable}." + Environment.NewLine);
        handler.Requests.ShouldAllSatisfy(request => request.PathAndQuery.ShouldBe("/graphql"));
    }

    [Fact]
    public async Task Publish_fails_and_writes_no_outputs_without_a_pull_request_url()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var publication = await CreatePublication(workspace, pullRequestUrl: string.Empty);
        using var handler = publication.Handler;

        // Act
        var result = await Run(workspace, publication, force: true);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldBe(string.Empty);
        result.Error.ShouldBe("sharedkernel-repo: Roadmap publication did not return a pull request URL." + Environment.NewLine);
        result.Outputs.ShouldBe(string.Empty);
        result.Summary.ShouldBe(string.Empty);
    }
}
