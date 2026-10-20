namespace SharedKernel.RepoConfig.Tests;

internal static class RoadmapTestContent
{
    public const string InvalidConfigJson = """
        {
          "schemaVersion": "1.0",
          "sourceOfTruth": "github",
          "itemIdPrefix": "RM",
          "allowed": {
            "types": [
              "epic"
            ],
            "statuses": []
          },
          "project": {
            "ordering": "rank",
            "blockedBy": "blocked",
            "closedStatuses": [
              "missing"
            ]
          },
          "scoring": {
            "model": "WSJF",
            "formula": "wrong"
          },
          "integrations": {
            "github": {
              "enabled": "yes"
            }
          }
        }
        """;

    public const string InvalidItemJson = """
        {
          "$schema": "../schema/roadmap-item.schema.json",
          "id": "BAD-001",
          "title": "Invalid roadmap item",
          "type": "mystery",
          "status": "unknown",
          "order": 0,
          "theme": "repo-operations",
          "outcome": "Invalid values should be reported.",
          "scoring": {
            "reach": -1,
            "impact": 7,
            "confidence": 0.01,
            "effort": 0
          },
          "blockedBy": "RM-002",
          "blocks": [],
          "dependencies": [],
          "tags": [],
          "labels": []
        }
        """;

    public const string IssueWithGitHubMappingJson = """
        {
          "$schema": "../schema/roadmap-item.schema.json",
          "id": "RM-002",
          "parent": "RM-001",
          "title": "Follow-up roadmap issue",
          "type": "issue",
          "status": "ready",
          "order": 20,
          "theme": "repo-operations",
          "outcome": "A follow-up issue is mapped to GitHub.",
          "scoring": {
            "reach": 10,
            "impact": 3,
            "confidence": 0.8,
            "effort": 2
          },
          "blockedBy": [],
          "blocks": [],
          "dependencies": [],
          "tags": [
            "roadmap"
          ],
          "labels": [
            "area: tooling"
          ],
          "integrations": {
            "github": {
              "issue": 997
            }
          }
        }
        """;

    public const string HigherPriorityIssueJson = """
        {
          "$schema": "../schema/roadmap-item.schema.json",
          "id": "RM-002",
          "parent": "RM-001",
          "title": "Higher priority roadmap issue",
          "type": "issue",
          "status": "ready",
          "order": 5,
          "theme": "repo-operations",
          "outcome": "An issue should appear before the epic in order.json.",
          "scoring": {
            "reach": 10,
            "impact": 3,
            "confidence": 0.8,
            "effort": 2
          },
          "blockedBy": [],
          "blocks": [],
          "dependencies": [],
          "tags": [
            "roadmap"
          ],
          "labels": [
            "area: tooling"
          ]
        }
        """;

    public const string UnblockedEnablerJson = """
        {
          "$schema": "../schema/roadmap-item.schema.json",
          "id": "RM-003",
          "parent": "RM-001",
          "title": "Unblocked enabler",
          "type": "enabler",
          "status": "ready",
          "order": 30,
          "theme": "repo-operations",
          "outcome": "An enabler remains available for project query tests.",
          "scoring": {
            "reach": 10,
            "impact": 4,
            "confidence": 0.9,
            "effort": 1
          },
          "blockedBy": [],
          "blocks": [],
          "dependencies": [],
          "tags": [
            "enabler"
          ],
          "labels": [
            "area: tooling"
          ]
        }
        """;

    public const string UntriagedIssueJson = """
        {
          "$schema": "../schema/roadmap-item.schema.json",
          "id": "RM-002",
          "parent": "RM-001",
          "title": "Untriaged roadmap issue",
          "type": "issue",
          "status": "ready",
          "triage": "untriaged",
          "theme": "repo-operations",
          "outcome": "An active item can be modeled before priority evidence is available.",
          "blockedBy": [],
          "blocks": [],
          "dependencies": [],
          "tags": [
            "roadmap"
          ],
          "labels": [
            "area: tooling"
          ],
          "integrations": {
            "github": {
              "issue": 997
            }
          }
        }
        """;

    public const string BlockedIssueJson = """
        {
          "$schema": "../schema/roadmap-item.schema.json",
          "id": "RM-002",
          "parent": "RM-001",
          "title": "Follow-up roadmap issue",
          "type": "issue",
          "status": "ready",
          "order": 20,
          "theme": "repo-operations",
          "outcome": "A follow-up issue waits for the epic to close.",
          "scoring": {
            "reach": 10,
            "impact": 3,
            "confidence": 0.8,
            "effort": 2
          },
          "blockedBy": [
            "RM-001"
          ],
          "blocks": [],
          "dependencies": [],
          "tags": [
            "roadmap"
          ],
          "labels": [
            "area: tooling"
          ]
        }
        """;
}
