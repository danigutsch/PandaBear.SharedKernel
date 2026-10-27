namespace PandaBear.RepoConfig.Tests;

/// <summary>Roadmap config and item JSON documents used as test inputs.</summary>
internal static class RoadmapTestContent
{
    /// <summary>A config with <c>sourceOfTruth</c> set to <c>github</c>, empty statuses, ordering <c>rank</c>, scoring model <c>WSJF</c>, and a string for GitHub <c>enabled</c>.</summary>
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

    /// <summary>An item with a wrong id prefix, unknown type and status, out-of-range scoring values, and a string where <c>blockedBy</c> needs an array.</summary>
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

    /// <summary>A ready issue <c>RM-002</c> under <c>RM-001</c>, order 20, mapped to GitHub issue 997 and with no blockers.</summary>
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

    /// <summary>A ready issue <c>RM-002</c> under <c>RM-001</c> with order 5, so it sorts ahead of its parent epic.</summary>
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

    /// <summary>A ready enabler <c>RM-003</c> under <c>RM-001</c> with order 30 and no blockers.</summary>
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

    /// <summary>A ready issue <c>RM-002</c> marked <c>untriaged</c>, mapped to GitHub issue 997, before priority evidence exists.</summary>
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

    /// <summary>A ready issue <c>RM-002</c> that is blocked by its parent epic <c>RM-001</c>.</summary>
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
