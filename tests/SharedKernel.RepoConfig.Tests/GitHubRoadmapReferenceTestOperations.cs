using System.Net;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Test helpers for the GitHub roadmap reference tests.</summary>
internal static class GitHubRoadmapReferenceTestOperations
{
    /// <summary>Queues the responses for an issue that moved to another repository, or for an unresolvable one when <paramref name="unresolved"/> is set.</summary>
    public static void EnqueueTransfer(TestHttpMessageHandler handler, bool unresolved = false)
    {
        handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse([]));
        handler.EnqueueJson(HttpStatusCode.OK, """{"data":{"repository":{"issueOrPullRequest":null}},"errors":[{"type":"NOT_FOUND","path":["repository","issueOrPullRequest"]}]}""");
        if (unresolved)
        {
            handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        }
        else
        {
            var redirect = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
            redirect.Headers.Location = new Uri("https://api.github.com/repos/other/library/issues/18");
            handler.Enqueue(redirect);
            handler.EnqueueJson(HttpStatusCode.OK, """{"number":18,"node_id":"I_external","html_url":"https://github.com/other/library/issues/18"}""");
        }

        handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateRepositoryCommitResponse());
    }
}
