using System.Net;

namespace SharedKernel.RepoConfig.Tests;

internal static class RoadmapGitHubPublicationTestOperations
{
    public const string BaseSha = RoadmapAutomationTestPlan.BaseSha;
    public const string BlobSha = "3333333333333333333333333333333333333333";
    public const string CommitSha = "5555555555555555555555555555555555555555";
    public const string DifferentSha = RoadmapAutomationTestPlan.DifferentSha;
    public const string PullRequestUrl = "https://github.com/owner/repository/pull/123";
    public const string TreeSha = "4444444444444444444444444444444444444444";
    public const string BaseTreeSha = "2222222222222222222222222222222222222222";

    public static RoadmapGitChangeSet CreateChanges(TemporaryRepoConfigWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        workspace.WriteFile("roadmap/items/RM-100.json", "{}\n");
        return RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Ordinary("roadmap/items/RM-100.json"));
    }

    public static void EnqueueObjectCreation(
        TestHttpMessageHandler handler,
        bool verified = true,
        string commitTreeSha = TreeSha,
        IReadOnlyList<string>? parentShas = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        parentShas ??= [BaseSha];
        var parents = string.Join(',', parentShas.Select(sha => $"{{\"sha\":\"{sha}\"}}"));
        EnqueueDefaultBranch(handler, BaseSha);
        handler.EnqueueJson(
            HttpStatusCode.OK,
            $"{{\"sha\":\"{BaseSha}\",\"tree\":{{\"sha\":\"{BaseTreeSha}\"}}}}");
        handler.EnqueueJson(HttpStatusCode.Created, $"{{\"sha\":\"{BlobSha}\"}}");
        handler.EnqueueJson(HttpStatusCode.Created, $"{{\"sha\":\"{TreeSha}\",\"truncated\":false,\"tree\":[]}}");
        handler.EnqueueJson(
            HttpStatusCode.Created,
            $"{{\"sha\":\"{CommitSha}\",\"tree\":{{\"sha\":\"{commitTreeSha}\"}},\"parents\":[{parents}],\"verification\":{{\"verified\":{(verified ? "true" : "false")},\"reason\":\"{(verified ? "valid" : "unsigned")}\"}}}}");
    }

    public static void EnqueueDefaultBranch(TestHttpMessageHandler handler, string sha)
    {
        ArgumentNullException.ThrowIfNull(handler);
        handler.EnqueueJson(HttpStatusCode.OK, $"{{\"object\":{{\"type\":\"commit\",\"sha\":\"{sha}\"}}}}");
    }

    public static void EnqueueCreatedReference(TestHttpMessageHandler handler) =>
        handler.EnqueueJson(
            HttpStatusCode.Created,
            $"{{\"ref\":\"refs/heads/{RoadmapAutomationTestPlan.Create().BranchName}\",\"object\":{{\"type\":\"commit\",\"sha\":\"{CommitSha}\"}}}}");

    public static void EnqueuePullRequest(
        TestHttpMessageHandler handler,
        string url = PullRequestUrl,
        string baseSha = BaseSha) =>
        handler.EnqueueJson(
            HttpStatusCode.Created,
            $"{{\"html_url\":\"{url}\",\"draft\":true,\"head\":{{\"sha\":\"{CommitSha}\"}},\"base\":{{\"sha\":\"{baseSha}\"}}}}");
}
