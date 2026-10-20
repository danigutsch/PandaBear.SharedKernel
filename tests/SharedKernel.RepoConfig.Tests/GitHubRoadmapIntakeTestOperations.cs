namespace SharedKernel.RepoConfig.Tests;

internal static class GitHubRoadmapIntakeTestOperations
{
    public static void Configure(TemporaryRepoConfigWorkspace workspace, string reconciliationManifest)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(reconciliationManifest);

        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        workspace.WriteFile("roadmap/reconciliation/open-issues-test.json", reconciliationManifest);
    }

    public static void AddImportedOpenItem(TemporaryRepoConfigWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        workspace.WriteFile(
            "roadmap/items/RM-018-github-100.json",
            """
            {
              "$schema": "../schema/roadmap-item.schema.json",
              "id": "RM-018",
              "title": "Previously imported issue",
              "type": "issue",
              "status": "proposed",
              "order": 11,
              "theme": "repo-operations",
              "outcome": "GitHub issue #100 is canonically represented from the reconciliation manifest.",
              "scoring": { "reach": 1, "impact": 1, "confidence": 0.1, "effort": 3 },
              "blockedBy": [],
              "blocks": [],
              "dependencies": [],
              "tags": [],
              "labels": [],
              "sources": [{ "kind": "github-issue", "reference": "#100" }],
              "integrations": { "github": { "issue": 100 } }
            }
            """);
        workspace.WriteFile(
            "roadmap/order.json",
            """
            {
              "ordering": "lower order values are higher priority",
              "items": ["RM-001", "RM-018"]
            }
            """);
    }

    public static void AddImportedOpenPairWithStaleRelationship(TemporaryRepoConfigWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        workspace.WriteFile(
            "roadmap/items/RM-018-github-100.json",
            """
            {
              "$schema": "../schema/roadmap-item.schema.json",
              "id": "RM-018",
              "title": "Imported parent",
              "type": "issue",
              "status": "proposed",
              "order": 11,
              "theme": "repo-operations",
              "outcome": "GitHub issue #100 is canonically represented from the reconciliation manifest.",
              "scoring": { "reach": 1, "impact": 1, "confidence": 0.1, "effort": 3 },
              "blockedBy": [],
              "blocks": ["RM-019"],
              "dependencies": [],
              "tags": [],
              "labels": [],
              "sources": [{ "kind": "github-issue", "reference": "#100" }],
              "integrations": { "github": { "issue": 100 } }
            }
            """);
        workspace.WriteFile(
            "roadmap/items/RM-019-github-101.json",
            """
            {
              "$schema": "../schema/roadmap-item.schema.json",
              "id": "RM-019",
              "title": "Imported child",
              "type": "issue",
              "status": "proposed",
              "order": 12,
              "theme": "repo-operations",
              "outcome": "GitHub issue #101 is canonically represented from the reconciliation manifest.",
              "scoring": { "reach": 1, "impact": 1, "confidence": 0.1, "effort": 3 },
              "blockedBy": ["RM-018"],
              "blocks": [],
              "dependencies": [],
              "tags": [],
              "labels": [],
              "sources": [{ "kind": "github-issue", "reference": "#101" }],
              "integrations": { "github": { "issue": 101 } }
            }
            """);
        workspace.WriteFile(
            "roadmap/order.json",
            """
            {
              "ordering": "lower order values are higher priority",
              "items": ["RM-001", "RM-018", "RM-019"]
            }
            """);
    }

    public static string ReadRoadmapState(TemporaryRepoConfigWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var roadmapPath = Path.Combine(workspace.RootPath, "roadmap");
        return string.Join(
            "\n---\n",
            Directory.EnumerateFiles(roadmapPath, "*.json", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(path => $"{Path.GetRelativePath(workspace.RootPath, path)}\n{File.ReadAllText(path)}"));
    }
}
