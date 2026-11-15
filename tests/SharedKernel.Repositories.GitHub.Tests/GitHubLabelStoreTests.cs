using Microsoft.Extensions.Time.Testing;

namespace PandaBear.Repositories.GitHub.Tests;

/// <summary>Engine tests split from the command-line tests in GitHubLabelSyncCommandTests.</summary>
public sealed class GitHubLabelStoreTests
{
    private const string Configuration = """
        {
          "version": 1,
          "labels": [
            { "name": "ci", "color": "1d76db", "description": "Continuous integration" },
            { "name": "type: epic", "color": "6f42c1", "description": "Parent issue" },
            { "name": "area: api", "color": "0e8a16", "description": "HTTP API contracts" },
            { "name": "dependencies", "color": "0366d6", "description": "" }
          ]
        }
        """;

    [Fact]
    public async Task Sync_labels_times_out_a_request_that_github_does_not_answer()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".config/sharedkernel-repo.json", Configuration);
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimingOutHttpMessageHandler(timeProvider);
        using var httpClient = new HttpClient(handler);
        using var connection = new GitHubConnection(httpClient);
        using var output = new InvariantStringWriter();
        var store = new GitHubLabelStore("owner/repository", connection, timeProvider);

        // Act
        var action = (Func<Task>)(async () => await RepositoryLabelSync.Run(
            workspace.RootPath, store, force: true, output, TestContext.Current.CancellationToken));
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("GitHub label sync timed out after 30 seconds.");
        output.ToString().ShouldBe(string.Empty);
    }
}
