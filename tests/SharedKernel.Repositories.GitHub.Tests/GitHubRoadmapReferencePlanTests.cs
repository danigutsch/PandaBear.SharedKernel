namespace SharedKernel.Repositories.GitHub.Tests;

/// <summary>Engine tests split from the command-line tests in GitHubRoadmapReferenceTests.</summary>
public sealed class GitHubRoadmapReferencePlanTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reconcile_rejects_ambiguous_identity_before_writing(bool nodeCollision)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        RoadmapInitializer.Initialize(workspace.RootPath);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        var original = workspace.ReadFile("roadmap/items/RM-018-github-100.json");
        var second = original.Replace("RM-018", "RM-019", StringComparison.Ordinal)
            .Replace("\"order\": 11", "\"order\": 12", StringComparison.Ordinal)
            .Replace("\"issue\": 100", "\"issue\": 101", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-019-github-101.json", second);
        workspace.WriteFile("roadmap/order.json", workspace.ReadFile("roadmap/order.json").Replace("\"RM-018\"", "\"RM-018\", \"RM-019\"", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        var snapshot = new GitHubRoadmapReconcileSnapshot(null, [])
        {
            ReferenceResolutions = new Dictionary<string, GitHubIssueReferenceResolution>(StringComparer.Ordinal)
            {
                ["RM-018"] = new("other/library", 18, "I_first", null, null),
                ["RM-019"] = new("other/library", nodeCollision ? 19 : 18, nodeCollision ? "I_first" : "I_second", null, null)
            }
        };
        Action action = () => GitHubRoadmapReferencePlan.Create(project, snapshot);

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("multiple roadmap", StringComparison.Ordinal);
        workspace.ReadFile("roadmap/items/RM-018-github-100.json").ShouldBe(original);
        workspace.ReadFile("roadmap/items/RM-019-github-101.json").ShouldBe(second);
    }
}
