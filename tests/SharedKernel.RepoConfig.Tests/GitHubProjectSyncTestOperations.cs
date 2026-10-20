using System.Net;

namespace SharedKernel.RepoConfig.Tests;

internal static class GitHubProjectSyncTestOperations
{
    public static void EnableProjectTarget(TemporaryRepoConfigWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace(
            "\"sourceOfTruth\": \"projection\"",
            "\"sourceOfTruth\": \"projection\",\n      \"projectV2\": { \"id\": \"project-id\", \"owner\": \"owner\", \"number\": 1 }",
            StringComparison.Ordinal));
    }

    public static void MapDefaultItem(TemporaryRepoConfigWorkspace workspace, int issueNumber = 997)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace(
            "\"labels\": [",
            $"\"integrations\": {{ \"github\": {{ \"issue\": {issueNumber} }} }},\n  \"labels\": [",
            StringComparison.Ordinal));
    }

    public static void EnqueueExistingProjectItem(
        TestHttpMessageHandler handler,
        string projectFields,
        string fieldValues)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFields);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldValues);

        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"labels\": [] }");
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, projectFields);
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"repository\": { \"issue\": { \"id\": \"issue-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [{ \"id\": \"item-id\", \"content\": { \"id\": \"issue-id\" } }], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, fieldValues);
    }

    public static void EnqueueExistingProjectPreflight(
        TestHttpMessageHandler handler,
        string projectFields,
        string fieldValues)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFields);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldValues);

        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, projectFields);
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"repository\": { \"issue\": { \"id\": \"issue-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [{ \"id\": \"item-id\", \"content\": { \"id\": \"issue-id\" } }], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, fieldValues);
    }

    public static void EnqueueMissingProjectPreflight(TestHttpMessageHandler handler, string projectFields)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFields);

        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, projectFields);
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"repository\": { \"issue\": { \"id\": \"issue-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
    }

    public static void EnqueueMissingProjectItem(
        TestHttpMessageHandler handler,
        string projectFields,
        string fieldValues)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFields);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldValues);

        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"labels\": [] }");
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, projectFields);
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"repository\": { \"issue\": { \"id\": \"issue-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"addProjectV2ItemById\": { \"item\": { \"id\": \"item-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, fieldValues);
    }

    public static string ProjectFieldsWithValidStatus(string additionalFields)
    {
        ArgumentNullException.ThrowIfNull(additionalFields);

        var fieldNodes = string.IsNullOrWhiteSpace(additionalFields)
            ? ValidStatusField
            : $"{additionalFields}, {ValidStatusField}";
        return $$"""
            { "data": { "node": { "fields": { "nodes": [{{fieldNodes}}] } } } }
            """;
    }

    public static string CompleteProjectFields(string statusOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statusOptions);

        return $$"""
            { "data": { "node": { "fields": { "nodes": [
              { "id": "order", "name": "Roadmap order", "dataType": "NUMBER" },
              { "id": "status", "name": "Roadmap status", "dataType": "SINGLE_SELECT", "options": {{statusOptions}} },
              { "id": "parent", "name": "Roadmap parent", "dataType": "TEXT" },
              { "id": "blocked", "name": "Roadmap blocked by", "dataType": "TEXT" },
              { "id": "tags", "name": "Roadmap tags", "dataType": "TEXT" },
              { "id": "reach", "name": "RICE reach", "dataType": "NUMBER" },
              { "id": "impact", "name": "RICE impact", "dataType": "NUMBER" },
              { "id": "confidence", "name": "RICE confidence", "dataType": "NUMBER" },
              { "id": "effort", "name": "RICE effort", "dataType": "NUMBER" },
              { "id": "score", "name": "RICE score", "dataType": "NUMBER" }
            ] } } } }
            """;
    }

    private const string ValidStatusField = """
        { "id": "status", "name": "Roadmap status", "dataType": "SINGLE_SELECT", "options": [
          { "id": "proposed", "name": "proposed" },
          { "id": "ready", "name": "ready" },
          { "id": "in-progress", "name": "in_progress" },
          { "id": "done", "name": "done" },
          { "id": "dropped", "name": "dropped" }
        ] }
        """;
}
