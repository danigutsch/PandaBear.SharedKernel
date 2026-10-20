using System.Security.Cryptography;
using System.Text.Json;

namespace SharedKernel.RepoConfig.Tool;

internal static class GitHubIssueSnapshotDigest
{
    public static string ComputeForOpenAndRequiredIssues(
        IEnumerable<GitHubRoadmapReconcileIssue> issues,
        IEnumerable<int> requiredIssueNumbers,
        string repository)
    {
        var required = requiredIssueNumbers.ToHashSet();
        var snapshot = issues.ToArray();
        foreach (var relation in snapshot
            .Where(issue => string.Equals(issue.State, "OPEN", StringComparison.Ordinal))
            .SelectMany(issue => issue.SubIssues
                .Concat(issue.BlockedBy)
                .Concat(issue.Blocking)
                .Concat(issue.Parent is null ? [] : [issue.Parent]))
            .Where(relation => string.Equals(relation.State, "CLOSED", StringComparison.Ordinal)
                && string.Equals(relation.Repository, repository, StringComparison.OrdinalIgnoreCase)))
        {
            required.Add(relation.Number);
        }

        return Compute(snapshot.Where(issue => string.Equals(issue.State, "OPEN", StringComparison.Ordinal)
            || required.Contains(issue.Number)));
    }

    public static string Compute(IEnumerable<GitHubRoadmapReconcileIssue> issues)
    {
        using var content = new MemoryStream();
        using var writer = new Utf8JsonWriter(content);
        writer.WriteStartArray();
        foreach (var issue in issues.OrderBy(issue => issue.Number))
        {
            writer.WriteStartObject();
            writer.WriteNumber("number", issue.Number);
            writer.WriteString("title", issue.Title);
            writer.WriteString("state", issue.State);
            WriteRelation(writer, "parent", issue.Parent);
            WriteStrings(writer, "labels", issue.Labels);
            WriteRelations(writer, "subIssues", issue.SubIssues);
            WriteRelations(writer, "blockedBy", issue.BlockedBy);
            WriteRelations(writer, "blocking", issue.Blocking);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.Flush();
        return Convert.ToHexStringLower(SHA256.HashData(content.ToArray()));
    }

    private static void WriteRelation(Utf8JsonWriter writer, string propertyName, GitHubRoadmapReconcileRelation? relation)
    {
        writer.WritePropertyName(propertyName);
        WriteRelationValue(writer, relation);
    }

    private static void WriteRelationValue(Utf8JsonWriter writer, GitHubRoadmapReconcileRelation? relation)
    {
        if (relation is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("repository", relation.Repository);
        writer.WriteNumber("number", relation.Number);
        writer.WriteString("state", relation.State);
        writer.WriteEndObject();
    }

    private static void WriteRelations(Utf8JsonWriter writer, string propertyName, IEnumerable<GitHubRoadmapReconcileRelation> relations)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var relation in relations
                     .OrderBy(relation => relation.Repository, StringComparer.Ordinal)
                     .ThenBy(relation => relation.Number)
                     .ThenBy(relation => relation.State, StringComparer.Ordinal))
        {
            WriteRelationValue(writer, relation);
        }

        writer.WriteEndArray();
    }

    private static void WriteStrings(Utf8JsonWriter writer, string propertyName, IEnumerable<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var value in values.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }
}
