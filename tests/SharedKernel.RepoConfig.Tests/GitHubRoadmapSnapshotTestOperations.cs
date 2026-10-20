using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SharedKernel.RepoConfig.Tests;

internal static class GitHubRoadmapSnapshotTestOperations
{
    public const string Repository = "owner/repository";

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

    public static void Enqueue(TestHttpMessageHandler handler, IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(snapshot);

        handler.Enqueue(CreateOpenIssuesResponse(snapshot));
        foreach (var issue in snapshot
            .Where(issue => string.Equals(issue.State, "CLOSED", StringComparison.Ordinal))
            .OrderBy(issue => issue.Number))
        {
            EnqueueIssue(handler, issue);
        }
    }

    public static HttpResponseMessage CreateOpenIssuesResponse(IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot)
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
            writer.WriteString("oid", "1111111111111111111111111111111111111111");
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

    public static GitHubRoadmapReconcileRelation Relation(int number, string state, string repository = Repository) =>
        new(number, repository, state);

    private static void WriteIssue(Utf8JsonWriter writer, GitHubRoadmapReconcileIssue issue)
    {
        writer.WriteStartObject();
        writer.WriteString("__typename", "Issue");
        writer.WriteNumber("number", issue.Number);
        writer.WriteString("title", issue.Title);
        writer.WriteString("state", issue.State);
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
