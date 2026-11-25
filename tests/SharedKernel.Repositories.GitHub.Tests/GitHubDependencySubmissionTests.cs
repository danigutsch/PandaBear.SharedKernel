using Microsoft.Extensions.Time.Testing;

namespace SharedKernel.Repositories.GitHub.Tests;

/// <summary>Engine tests split from the command-line tests in DependencySubmitCommandTests.</summary>
public sealed class GitHubDependencySubmissionTests
{
    private const string SnapshotPath = "TestResults/dependency-snapshot.json";
    private const string Snapshot = """{"version":0,"sha":"0123456789abcdef0123456789abcdef01234567","ref":"refs/heads/main","manifests":{}}""";

    [Fact]
    public async Task Submit_times_out_a_request_that_github_does_not_answer()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(SnapshotPath, Snapshot);
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimingOutHttpMessageHandler(timeProvider);
        using var httpClient = new HttpClient(handler);
        using var connection = new GitHubConnection(httpClient);
        using var output = new InvariantStringWriter();
        var options = new DependencySubmitOptions(workspace.RootPath, SnapshotPath, "owner/repository", Force: true);

        // Act
        var action = (Func<Task>)(async () => await GitHubDependencySubmission.Run(options, connection, timeProvider, output, TestContext.Current.CancellationToken));
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("GitHub dependency submission timed out after 30 seconds.");
        output.ToString().ShouldBe(string.Empty);
    }
}
