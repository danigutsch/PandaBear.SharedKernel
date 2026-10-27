using System.Net;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Fixed object ids and scripted GitHub REST responses for roadmap publication tests.</summary>
internal static class RoadmapGitHubPublicationTestOperations
{
    /// <summary>The default branch head the publication is based on.</summary>
    public const string BaseSha = RoadmapAutomationTestPlan.BaseSha;

    /// <summary>The SHA returned when the blob is created.</summary>
    public const string BlobSha = "3333333333333333333333333333333333333333";

    /// <summary>The SHA returned when the commit is created.</summary>
    public const string CommitSha = "5555555555555555555555555555555555555555";

    /// <summary>A SHA that differs from <see cref="BaseSha"/>, for a default branch that moved.</summary>
    public const string DifferentSha = RoadmapAutomationTestPlan.DifferentSha;

    /// <summary>The URL of the pull request returned by the scripted create-pull-request response.</summary>
    public const string PullRequestUrl = "https://github.com/owner/repository/pull/123";

    /// <summary>The SHA returned when the new tree is created.</summary>
    public const string TreeSha = "4444444444444444444444444444444444444444";

    /// <summary>The tree SHA of the base commit.</summary>
    public const string BaseTreeSha = "2222222222222222222222222222222222222222";

    /// <summary>Writes <c>roadmap/items/RM-100.json</c> in the workspace and captures it as the only changed file.</summary>
    /// <param name="workspace">The workspace that receives the file.</param>
    public static RoadmapGitChangeSet CreateChanges(TemporaryRepoConfigWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        workspace.WriteFile("roadmap/items/RM-100.json", "{}\n");
        return RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Ordinary("roadmap/items/RM-100.json"));
    }

    /// <summary>Queues the responses for reading the default branch and base commit and creating the blob, tree, and commit.</summary>
    /// <param name="handler">The handler that receives the queued responses.</param>
    /// <param name="verified">Whether the created commit reports a valid signature; otherwise it reports <c>unsigned</c>.</param>
    /// <param name="commitTreeSha">The tree SHA the created commit reports.</param>
    /// <param name="parentShas">The parent SHAs the created commit reports, or <see langword="null"/> for <see cref="BaseSha"/> only.</param>
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

    /// <summary>Queues a response that reports the default branch pointing at commit <paramref name="sha"/>.</summary>
    /// <param name="handler">The handler that receives the queued response.</param>
    /// <param name="sha">The commit SHA the branch reference reports.</param>
    public static void EnqueueDefaultBranch(TestHttpMessageHandler handler, string sha)
    {
        ArgumentNullException.ThrowIfNull(handler);
        handler.EnqueueJson(HttpStatusCode.OK, $"{{\"object\":{{\"type\":\"commit\",\"sha\":\"{sha}\"}}}}");
    }

    /// <summary>Queues a <c>201 Created</c> response for the new automation branch reference pointing at <see cref="CommitSha"/>.</summary>
    /// <param name="handler">The handler that receives the queued response.</param>
    public static void EnqueueCreatedReference(TestHttpMessageHandler handler) =>
        handler.EnqueueJson(
            HttpStatusCode.Created,
            $"{{\"ref\":\"refs/heads/{RoadmapAutomationTestPlan.Create().BranchName}\",\"object\":{{\"type\":\"commit\",\"sha\":\"{CommitSha}\"}}}}");

    /// <summary>Queues a <c>201 Created</c> response for a draft pull request whose head is <see cref="CommitSha"/>.</summary>
    /// <param name="handler">The handler that receives the queued response.</param>
    /// <param name="url">The pull request URL reported by the response.</param>
    /// <param name="baseSha">The base SHA reported by the response.</param>
    public static void EnqueuePullRequest(
        TestHttpMessageHandler handler,
        string url = PullRequestUrl,
        string baseSha = BaseSha) =>
        handler.EnqueueJson(
            HttpStatusCode.Created,
            $"{{\"html_url\":\"{url}\",\"draft\":true,\"head\":{{\"sha\":\"{CommitSha}\"}},\"base\":{{\"sha\":\"{baseSha}\"}}}}");
}
