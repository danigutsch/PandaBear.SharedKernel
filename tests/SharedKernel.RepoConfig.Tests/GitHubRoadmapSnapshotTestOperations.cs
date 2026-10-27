using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Test helpers that build GitHub roadmap snapshots and the HTTP responses that serve them.</summary>
internal static class GitHubRoadmapSnapshotTestOperations
{
    /// <summary>The repository that the fake snapshot describes.</summary>
    public const string Repository = "owner/repository";
    /// <summary>The default branch commit identifier that the fake responses report.</summary>
    public const string DefaultOid = "1111111111111111111111111111111111111111";

    /// <summary>Writes a reconciliation manifest whose snapshot digest is computed from the given snapshot.</summary>
    public static void Configure(
        TemporaryRepoConfigWorkspace workspace,
        string reconciliationManifest,
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(reconciliationManifest);
        ArgumentNullException.ThrowIfNull(snapshot);

        var root = JsonNode.Parse(reconciliationManifest)?.AsObject()
            ?? throw new InvalidOperationException("Test reconciliation manifest must be a JSON object.");
        root["snapshotDigest"] = GitHubIssueSnapshotDigest.Compute(snapshot);
        GitHubRoadmapIntakeTestOperations.Configure(
            workspace,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Queues the open issues page, the lookups of closed issues, and the default branch commit that serve the snapshot.</summary>
    public static void Enqueue(
        TestHttpMessageHandler handler,
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot,
        string oid = DefaultOid)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(snapshot);

        handler.Enqueue(CreateOpenIssuesResponse(snapshot, oid));
        foreach (var issue in snapshot
            .Where(issue => string.Equals(issue.State, "CLOSED", StringComparison.Ordinal))
            .OrderBy(issue => issue.Number))
        {
            EnqueueIssue(handler, issue);
        }

        handler.Enqueue(CreateRepositoryCommitResponse(oid));
    }

    /// <summary>Creates the GraphQL response that lists the open issues of the snapshot.</summary>
    public static HttpResponseMessage CreateOpenIssuesResponse(
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot,
        string oid = DefaultOid)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("data");
            writer.WriteStartObject();
            writer.WritePropertyName("repository");
            writer.WriteStartObject();
            writer.WritePropertyName("defaultBranchRef");
            writer.WriteStartObject();
            writer.WritePropertyName("target");
            writer.WriteStartObject();
            writer.WriteString("oid", oid);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WritePropertyName("issues");
            writer.WriteStartObject();
            writer.WritePropertyName("nodes");
            writer.WriteStartArray();
            foreach (var issue in snapshot
                .Where(issue => string.Equals(issue.State, "OPEN", StringComparison.Ordinal))
                .OrderBy(issue => issue.Number))
            {
                WriteIssue(writer, issue);
            }

            writer.WriteEndArray();
            WritePageInfo(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(System.Text.Encoding.UTF8.GetString(stream.ToArray()), System.Text.Encoding.UTF8, "application/json")
        };
    }

    /// <summary>Creates the GraphQL response that reports the default branch commit.</summary>
    public static HttpResponseMessage CreateRepositoryCommitResponse(string oid = DefaultOid) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"data\":{\"repository\":{\"defaultBranchRef\":{\"target\":{\"oid\":\"" + oid + "\"}}}}}",
                System.Text.Encoding.UTF8,
                "application/json")
        };

    /// <summary>Creates a snapshot issue, defaulting the labels and relations to empty.</summary>
    public static GitHubRoadmapReconcileIssue Issue(
        int number,
        string title,
        string state,
        IReadOnlyList<string>? labels = null,
        GitHubRoadmapReconcileRelation? parent = null,
        IReadOnlyList<GitHubRoadmapReconcileRelation>? subIssues = null,
        IReadOnlyList<GitHubRoadmapReconcileRelation>? blockedBy = null,
        IReadOnlyList<GitHubRoadmapReconcileRelation>? blocking = null) => new(
            number,
            title,
            state,
            labels ?? [],
            parent,
            subIssues ?? [],
            blockedBy ?? [],
            blocking ?? []);

    /// <summary>Creates a relation to an issue, in <see cref="Repository"/> unless another repository is given.</summary>
    public static GitHubRoadmapReconcileRelation Relation(int number, string state, string repository = Repository) =>
        new(number, repository, state);

    private static void WriteIssue(Utf8JsonWriter writer, GitHubRoadmapReconcileIssue issue)
    {
        writer.WriteStartObject();
        writer.WriteString("__typename", "Issue");
        if (issue.NodeId is not null)
        {
            writer.WriteString("id", issue.NodeId);
        }

        writer.WriteNumber("number", issue.Number);
        writer.WriteString("title", issue.Title);
        writer.WriteString("state", issue.State);
        if (issue.StateReason is not null)
        {
            writer.WriteString("stateReason", issue.StateReason);
        }

        writer.WritePropertyName("labels");
        writer.WriteStartObject();
        writer.WritePropertyName("nodes");
        writer.WriteStartArray();
        foreach (var label in issue.Labels)
        {
            writer.WriteStartObject();
            writer.WriteString("name", label);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WritePageInfo(writer);
        writer.WriteEndObject();
        writer.WritePropertyName("parent");
        if (issue.Parent is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            WriteRelation(writer, issue.Parent);
        }

        WriteRelations(writer, "subIssues", issue.SubIssues);
        WriteRelations(writer, "blockedBy", issue.BlockedBy);
        WriteRelations(writer, "blocking", issue.Blocking);
        writer.WriteEndObject();
    }

    private static void EnqueueIssue(TestHttpMessageHandler handler, GitHubRoadmapReconcileIssue issue)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("data");
            writer.WriteStartObject();
            writer.WritePropertyName("repository");
            writer.WriteStartObject();
            writer.WritePropertyName("issueOrPullRequest");
            WriteIssue(writer, issue);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        handler.EnqueueJson(HttpStatusCode.OK, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static void WriteRelations(Utf8JsonWriter writer, string propertyName, IReadOnlyList<GitHubRoadmapReconcileRelation> relations)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartObject();
        writer.WritePropertyName("nodes");
        writer.WriteStartArray();
        foreach (var relation in relations)
        {
            WriteRelation(writer, relation);
        }

        writer.WriteEndArray();
        WritePageInfo(writer);
        writer.WriteEndObject();
    }

    private static void WriteRelation(Utf8JsonWriter writer, GitHubRoadmapReconcileRelation relation)
    {
        writer.WriteStartObject();
        writer.WriteNumber("number", relation.Number);
        writer.WriteString("state", relation.State);
        writer.WritePropertyName("repository");
        writer.WriteStartObject();
        writer.WriteString("nameWithOwner", relation.Repository);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WritePageInfo(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("pageInfo");
        writer.WriteStartObject();
        writer.WriteBoolean("hasNextPage", false);
        writer.WriteNull("endCursor");
        writer.WriteEndObject();
    }
}
