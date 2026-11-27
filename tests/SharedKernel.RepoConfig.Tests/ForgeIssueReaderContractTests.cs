using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SharedKernel.Repositories.Forgejo;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Runs the common read-port assertions against both forge protocols.</summary>
public sealed class ForgeIssueReaderContractTests
{
    private const string Repository = "owner/repository";
    private const string BlockerRepository = "owner/other";
    private static readonly Uri ForgejoServer = new("https://forge.example");
    private static readonly ProductInfoHeaderValue UserAgent = new("sharedkernel-test", "1.0");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_open_issue_preserves_labels_and_the_identity_and_state_of_cross_repository_blockers(bool useForgejo)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        using var http = new HttpClient(handler, disposeHandler: false);
        using var forgejo = useForgejo ? new ForgejoIssueReader(handler, ForgejoServer, UserAgent, null) : null;
        IForgeIssueReader reader = forgejo is null ? new GitHubForgeIssueReader(http) : forgejo;
        if (useForgejo)
        {
            var list = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    [{"number":4,"title":"Example","state":"open","labels":[{"name":"ready"}],"repository":{"full_name":"owner/repository"}}]
                    """)
            };
            list.Headers.Add("X-Total-Count", "1");
            handler.Enqueue(list);
            handler.EnqueueJson(HttpStatusCode.OK, """[{"number":12,"state":"closed","repository":{"full_name":"owner/other"}}]""");
            handler.EnqueueJson(HttpStatusCode.OK, "[]");
        }
        else
        {
            handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse(
            [
                GitHubRoadmapSnapshotTestOperations.Issue(4, "Example", "OPEN", labels: ["ready"],
                    blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(12, "CLOSED", BlockerRepository)])
            ]));
        }

        // Act
        var issues = await reader.ReadOpenIssues(Repository, TestContext.Current.CancellationToken);

        // Assert
        var issue = issues.ShouldHaveSingleItem();
        issue.Reference.ShouldBe(new ForgeIssueReference(Repository, 4));
        issue.IsOpen.ShouldBeTrue();
        issue.Labels.ShouldBe(["ready"]);
        issue.BlockedBy.ShouldBe([new ForgeIssueLink(new ForgeIssueReference(BlockerRepository, 12), false)]);
        if (useForgejo)
        {
            handler.Requests.ShouldAllSatisfy(static request => request.Method.ShouldBe(HttpMethod.Get));
        }
        else
        {
            var request = handler.Requests.ShouldHaveSingleItem();
            using var payload = JsonDocument.Parse(request.Body!);
            payload.RootElement.GetProperty("query").GetString()!.ShouldStartWith("query(", StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_inaccessible_issue_reports_the_reference_instead_of_returning_an_issue(bool useForgejo)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.NotFound, """{"message":"Not Found"}""");
        using var http = new HttpClient(handler, disposeHandler: false);
        using var forgejo = useForgejo ? new ForgejoIssueReader(handler, ForgejoServer, UserAgent, null) : null;
        IForgeIssueReader reader = forgejo is null ? new GitHubForgeIssueReader(http) : forgejo;

        // Act
        var reference = new ForgeIssueReference(BlockerRepository, 12);
        Func<Task> read = async () => _ = await reader.ReadIssue(reference, TestContext.Current.CancellationToken);
        var error = await read.ShouldThrow<InvalidOperationException>();

        // Assert
        error.Message.ShouldContain("owner/other#12", StringComparison.Ordinal);
        error.Message.ShouldContain("404", StringComparison.Ordinal);
    }
}
