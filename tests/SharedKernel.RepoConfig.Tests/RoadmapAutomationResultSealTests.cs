namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RoadmapAutomationResultSealTests
{
    [Fact]
    public void Result_seal_round_trip_binds_plan_artifact_base_snapshot_and_diff()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var path = Path.Combine(workspace.RootPath, "result-seal.json");
        var plan = RoadmapAutomationTestPlan.Create();
        var artifactDigest = new string('c', 64);
        var resultDigest = new string('d', 64);
        var seal = RoadmapAutomationResultSeal.Create(
            plan,
            artifactDigest,
            resultDigest,
            pathCount: 3,
            hasChanges: true);

        // Act
        seal.Write(path);
        var loaded = RoadmapAutomationResultSeal.Load(
            path,
            plan.PlanDigest,
            artifactDigest,
            resultDigest);

        // Assert
        loaded.PlanDigest.ShouldBe(plan.PlanDigest);
        loaded.ArtifactDigest.ShouldBe(artifactDigest);
        loaded.BaseSha.ShouldBe(plan.BaseSha);
        loaded.SnapshotDigest.ShouldBe(plan.SnapshotDigest);
        loaded.ResultDigest.ShouldBe(resultDigest);
        loaded.PathCount.ShouldBe(3);
        loaded.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public void Result_seal_rejects_tampering()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var path = Path.Combine(workspace.RootPath, "result-seal.json");
        var plan = RoadmapAutomationTestPlan.Create();
        var artifactDigest = new string('c', 64);
        var resultDigest = new string('d', 64);
        var seal = RoadmapAutomationResultSeal.Create(
            plan,
            artifactDigest,
            resultDigest,
            pathCount: 1,
            hasChanges: true);
        seal.Write(path);
        var content = File.ReadAllText(path).Replace(resultDigest, new string('e', 64), StringComparison.Ordinal);
        File.WriteAllText(path, content);

        // Act
        Action action = () => RoadmapAutomationResultSeal.Load(
            path,
            plan.PlanDigest,
            artifactDigest,
            resultDigest);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation result seal is invalid or was tampered with.");
    }

    [Fact]
    public void Result_seal_write_refuses_to_replace_an_existing_artifact()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var path = Path.Combine(workspace.RootPath, "result-seal.json");
        File.WriteAllText(path, "existing-artifact");
        var seal = RoadmapAutomationResultSeal.Create(
            RoadmapAutomationTestPlan.Create(),
            new string('c', 64),
            new string('d', 64),
            pathCount: 1,
            hasChanges: true);
        Action action = () => seal.Write(path);

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();
        var content = File.ReadAllText(path);

        // Assert
        exception.Message.ShouldContain("changed after the write plan was created", StringComparison.Ordinal);
        content.ShouldBe("existing-artifact");
    }
}
