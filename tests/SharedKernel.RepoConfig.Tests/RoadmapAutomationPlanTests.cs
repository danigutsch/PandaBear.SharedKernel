namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RoadmapAutomationPlanTests
{
    [Fact]
    public void Plan_round_trip_is_canonical_minimal_and_integrity_bound()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var path = Path.Combine(workspace.RootPath, "roadmap-plan.json");
        var plan = RoadmapAutomationPlan.Create(
            "owner/repository",
            runId: 42,
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.SnapshotDigest,
            RoadmapAutomationTestPlan.InputDigest,
            "roadmap/reconciliation/open-issues-test.json",
            new Dictionary<int, string> { [90] = "RM-090" },
            [
                new RoadmapClosedItemTransition(101, "RM-101"),
                new RoadmapClosedItemTransition(100, "RM-100")
            ],
            snapshotChanged: false);

        // Act
        plan.Write(path);
        var loaded = RoadmapAutomationPlan.Load(path, plan.PlanDigest);
        var json = File.ReadAllText(path);

        // Assert
        loaded.PlanDigest.ShouldBe(plan.PlanDigest);
        loaded.BranchName.ShouldBe($"roadmap-automation/run-42-{RoadmapAutomationTestPlan.BaseSha[..12]}");
        loaded.RequiredClosedTransitions.Select(transition => transition.Issue).ShouldBe([100, 101]);
        loaded.HasChanges.ShouldBeTrue();
        json.ShouldNotContain("title", StringComparison.OrdinalIgnoreCase);
        json.ShouldNotContain("labels", StringComparison.OrdinalIgnoreCase);
        json.ShouldNotContain("body", StringComparison.OrdinalIgnoreCase);
        json.ShouldNotContain("comments", StringComparison.OrdinalIgnoreCase);
        json.ShouldNotContain("token", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plan_load_rejects_tampered_content()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var path = Path.Combine(workspace.RootPath, "roadmap-plan.json");
        var plan = RoadmapAutomationTestPlan.Create();
        plan.Write(path);
        var content = File.ReadAllText(path).Replace(
            RoadmapAutomationTestPlan.SnapshotDigest,
            new string('c', 64),
            StringComparison.Ordinal);
        File.WriteAllText(path, content);

        // Act
        Action action = () => RoadmapAutomationPlan.Load(path, plan.PlanDigest);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation plan digest does not match its content.");
    }

    [Fact]
    public void Plan_write_refuses_to_replace_an_existing_artifact()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        var path = Path.Combine(workspace.RootPath, "roadmap-plan.json");
        File.WriteAllText(path, "existing-artifact");
        var plan = RoadmapAutomationTestPlan.Create();
        Action action = () => plan.Write(path);

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();
        var content = File.ReadAllText(path);

        // Assert
        exception.Message.ShouldContain("changed after the write plan was created", StringComparison.Ordinal);
        content.ShouldBe("existing-artifact");
    }

    [Fact]
    public void Plan_write_rejects_an_artifact_directory_symbolic_link()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        var artifactDirectory = Path.Combine(workspace.RootPath, "artifacts");
        Directory.CreateSymbolicLink(artifactDirectory, outside.RootPath);
        var path = Path.Combine(artifactDirectory, "roadmap-plan.json");
        var plan = RoadmapAutomationTestPlan.Create();
        Action action = () => plan.Write(path);

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("symbolic link", StringComparison.Ordinal);
        File.Exists(Path.Combine(outside.RootPath, "roadmap-plan.json")).ShouldBeFalse();
    }

    [Fact]
    public void Before_write_validation_rejects_a_stale_base_sha()
    {
        // Arrange
        var plan = RoadmapAutomationTestPlan.Create();

        // Act
        Action action = () => RoadmapAutomationPlanValidator.ValidateBeforeWrite(
            plan,
            plan.PlanDigest,
            "owner/repository",
            RoadmapAutomationTestPlan.DifferentSha,
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.SnapshotDigest,
            RoadmapAutomationTestPlan.InputDigest,
            new Dictionary<int, string> { [90] = "RM-090" },
            [new RoadmapClosedItemTransition(100, "RM-100")]);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation plan base SHA is stale.");
    }

    [Fact]
    public void Before_write_validation_rejects_a_moved_default_branch()
    {
        // Arrange
        var plan = RoadmapAutomationTestPlan.Create();

        // Act
        Action action = () => RoadmapAutomationPlanValidator.ValidateBeforeWrite(
            plan,
            plan.PlanDigest,
            "owner/repository",
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.DifferentSha,
            RoadmapAutomationTestPlan.SnapshotDigest,
            RoadmapAutomationTestPlan.InputDigest,
            new Dictionary<int, string> { [90] = "RM-090" },
            [new RoadmapClosedItemTransition(100, "RM-100")]);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation plan default-branch OID is stale.");
    }

    [Fact]
    public void Before_write_validation_rejects_a_changed_github_snapshot()
    {
        // Arrange
        var plan = RoadmapAutomationTestPlan.Create();

        // Act
        Action action = () => RoadmapAutomationPlanValidator.ValidateBeforeWrite(
            plan,
            plan.PlanDigest,
            "owner/repository",
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.BaseSha,
            new string('c', 64),
            RoadmapAutomationTestPlan.InputDigest,
            new Dictionary<int, string> { [90] = "RM-090" },
            [new RoadmapClosedItemTransition(100, "RM-100")]);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation GitHub snapshot is stale.");
    }

    [Fact]
    public void Before_write_validation_rejects_changed_repository_inputs()
    {
        // Arrange
        var plan = RoadmapAutomationTestPlan.Create();

        // Act
        Action action = () => RoadmapAutomationPlanValidator.ValidateBeforeWrite(
            plan,
            plan.PlanDigest,
            "owner/repository",
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.SnapshotDigest,
            new string('c', 64),
            new Dictionary<int, string> { [90] = "RM-090" },
            [new RoadmapClosedItemTransition(100, "RM-100")]);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation repository input snapshot is stale.");
    }

    [Fact]
    public void Before_write_validation_requires_the_exact_candidate_transitions()
    {
        // Arrange
        var plan = RoadmapAutomationTestPlan.Create();

        // Act
        Action action = () => RoadmapAutomationPlanValidator.ValidateBeforeWrite(
            plan,
            plan.PlanDigest,
            "owner/repository",
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.SnapshotDigest,
            RoadmapAutomationTestPlan.InputDigest,
            new Dictionary<int, string> { [90] = "RM-090" },
            [new RoadmapClosedItemTransition(101, "RM-101")]);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation closure candidates changed after approval.");
    }

    [Fact]
    public void Plan_without_snapshot_or_transition_changes_is_idempotent()
    {
        // Arrange
        var plan = RoadmapAutomationTestPlan.Create([], snapshotChanged: false);

        // Act
        RoadmapAutomationPlanValidator.ValidateBeforeWrite(
            plan,
            plan.PlanDigest,
            "owner/repository",
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.SnapshotDigest,
            RoadmapAutomationTestPlan.InputDigest,
            new Dictionary<int, string> { [90] = "RM-090" },
            []);

        // Assert
        plan.HasChanges.ShouldBeFalse();
        plan.RequiredClosedTransitions.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_rejects_untrusted_transition_metadata()
    {
        // Act
        Action action = () => RoadmapAutomationPlan.Create(
            "owner/repository",
            runId: 42,
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.SnapshotDigest,
            RoadmapAutomationTestPlan.InputDigest,
            "roadmap/reconciliation/open-issues-test.json",
            new Dictionary<int, string>(),
            [new RoadmapClosedItemTransition(100, "RM-100\n::warning::unsafe")],
            snapshotChanged: false);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation plan contains an invalid roadmap item ID.");
    }

    [Fact]
    public void Transition_writer_adds_only_the_exact_approved_union_idempotently()
    {
        // Arrange
        const string manifest = """
            {
              "repository": "owner/repository",
              "closedItemTransitions": [
                { "issue": 90, "roadmapItem": "RM-090" }
              ],
              "needsHuman": []
            }
            """;
        RoadmapClosedItemTransition[] approved = [new(100, "RM-100")];

        // Act
        var first = RoadmapClosedTransitionWriter.AddToManifest(manifest, approved);
        var second = RoadmapClosedTransitionWriter.AddToManifest(first, approved);
        using var document = System.Text.Json.JsonDocument.Parse(first);
        var transitions = document.RootElement.GetProperty("closedItemTransitions")
            .EnumerateArray()
            .Select(transition => (
                transition.GetProperty("issue").GetInt32(),
                transition.GetProperty("roadmapItem").GetString()))
            .ToArray();

        // Assert
        transitions.ShouldBe([(90, "RM-090"), (100, "RM-100")]);
        second.ShouldBe(first);
        first.ShouldContain("\"needsHuman\": []", StringComparison.Ordinal);
    }

    [Fact]
    public void Before_push_validation_rejects_extra_closure_approvals()
    {
        // Arrange
        var plan = RoadmapAutomationTestPlan.Create();
        var currentTransitions = new Dictionary<int, string>
        {
            [90] = "RM-090",
            [100] = "RM-100",
            [101] = "RM-101"
        };

        // Act
        Action action = () => RoadmapAutomationPlanValidator.ValidateBeforePush(
            plan,
            plan.PlanDigest,
            "owner/repository",
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.SnapshotDigest,
            currentTransitions,
            []);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation closure approvals changed after the protected apply.");
    }

    [Fact]
    public void Before_push_validation_rechecks_every_approved_live_anchor()
    {
        // Arrange
        var plan = RoadmapAutomationTestPlan.Create();
        var approvedTransitions = new Dictionary<int, string>
        {
            [90] = "RM-090",
            [100] = "RM-100"
        };
        Action changedPlanDigest = () => RoadmapAutomationPlanValidator.ValidateBeforePush(
            plan,
            new string('c', 64),
            "owner/repository",
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.SnapshotDigest,
            approvedTransitions,
            []);
        Action staleBase = () => RoadmapAutomationPlanValidator.ValidateBeforePush(
            plan,
            plan.PlanDigest,
            "owner/repository",
            RoadmapAutomationTestPlan.DifferentSha,
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.SnapshotDigest,
            approvedTransitions,
            []);
        Action movedDefaultBranch = () => RoadmapAutomationPlanValidator.ValidateBeforePush(
            plan,
            plan.PlanDigest,
            "owner/repository",
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.DifferentSha,
            RoadmapAutomationTestPlan.SnapshotDigest,
            approvedTransitions,
            []);
        Action changedSnapshot = () => RoadmapAutomationPlanValidator.ValidateBeforePush(
            plan,
            plan.PlanDigest,
            "owner/repository",
            RoadmapAutomationTestPlan.BaseSha,
            RoadmapAutomationTestPlan.BaseSha,
            new string('c', 64),
            approvedTransitions,
            []);

        // Act
        var planException = changedPlanDigest.ShouldThrow<InvalidOperationException>();
        var baseException = staleBase.ShouldThrow<InvalidOperationException>();
        var defaultBranchException = movedDefaultBranch.ShouldThrow<InvalidOperationException>();
        var snapshotException = changedSnapshot.ShouldThrow<InvalidOperationException>();

        // Assert
        planException.Message.ShouldBe("Roadmap automation plan digest changed after approval.");
        baseException.Message.ShouldBe("Roadmap automation plan base SHA is stale.");
        defaultBranchException.Message.ShouldBe("Roadmap automation plan default-branch OID is stale.");
        snapshotException.Message.ShouldBe("Roadmap automation GitHub snapshot is stale.");
    }
}
