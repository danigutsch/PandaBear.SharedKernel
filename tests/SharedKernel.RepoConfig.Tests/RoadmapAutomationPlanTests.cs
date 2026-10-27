namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests creating, writing, loading, and validating roadmap automation plans and their closed-item transitions.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RoadmapAutomationPlanTests
{
    /// <summary>A written plan reloads with the same digest, a branch named from the run id and the first 12 characters of the base SHA, and required transitions sorted to 100 then 101, and its JSON contains no title, labels, body, comments, or token text.</summary>
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
            new RoadmapClosedItemTransition[] { new(90, "RM-090") },
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

    /// <summary>Loading a plan file whose snapshot digest was replaced throws that the plan digest does not match its content.</summary>
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

    /// <summary>Writing a plan to a path that already holds a file throws that the file changed after the write plan was created and leaves the existing content untouched.</summary>
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

    /// <summary>Writing a plan into an artifact directory that is a symbolic link to another directory throws a symbolic-link error and creates no file at the link target.</summary>
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

    /// <summary>Validating before write throws that the checkout is not the default-branch head when the checkout SHA differs from the plan's base and the default branch is still at the base.</summary>
    [Fact]
    public void Before_write_validation_rejects_a_checkout_ahead_of_the_default_branch()
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
            new RoadmapClosedItemTransition[] { new(90, "RM-090") },
            [new RoadmapClosedItemTransition(100, "RM-100")]);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation checkout is not the current default-branch head.");
    }

    /// <summary>Validating before write succeeds when both the checkout and the default branch have moved to a new SHA but the snapshot and input digests still match the plan.</summary>
    [Fact]
    public void Before_write_validation_accepts_a_newer_head_when_the_repository_inputs_are_unchanged()
    {
        // Arrange
        var plan = RoadmapAutomationTestPlan.Create();

        // Act
        RoadmapAutomationPlanValidator.ValidateBeforeWrite(
            plan,
            plan.PlanDigest,
            "owner/repository",
            RoadmapAutomationTestPlan.DifferentSha,
            RoadmapAutomationTestPlan.DifferentSha,
            RoadmapAutomationTestPlan.SnapshotDigest,
            RoadmapAutomationTestPlan.InputDigest,
            new RoadmapClosedItemTransition[] { new(90, "RM-090") },
            [new RoadmapClosedItemTransition(100, "RM-100")]);

        // Assert
        plan.BaseSha.ShouldBe(RoadmapAutomationTestPlan.BaseSha);
    }

    /// <summary>Validating before write throws that the repository input snapshot is stale when the head moved and the input digest no longer matches the plan.</summary>
    [Fact]
    public void Before_write_validation_rejects_a_newer_head_when_the_repository_inputs_changed()
    {
        // Arrange
        var plan = RoadmapAutomationTestPlan.Create();

        // Act
        Action action = () => RoadmapAutomationPlanValidator.ValidateBeforeWrite(
            plan,
            plan.PlanDigest,
            "owner/repository",
            RoadmapAutomationTestPlan.DifferentSha,
            RoadmapAutomationTestPlan.DifferentSha,
            RoadmapAutomationTestPlan.SnapshotDigest,
            new string('c', 64),
            new RoadmapClosedItemTransition[] { new(90, "RM-090") },
            [new RoadmapClosedItemTransition(100, "RM-100")]);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation repository input snapshot is stale.");
    }

    /// <summary>Validating before write throws that the checkout is not the default-branch head when the checkout is at the plan's base but the default branch has moved to another SHA.</summary>
    [Fact]
    public void Before_write_validation_rejects_a_checkout_behind_the_default_branch()
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
            new RoadmapClosedItemTransition[] { new(90, "RM-090") },
            [new RoadmapClosedItemTransition(100, "RM-100")]);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation checkout is not the current default-branch head.");
    }

    /// <summary>Validating before write throws that the GitHub snapshot is stale when the current snapshot digest differs from the plan's.</summary>
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
            new RoadmapClosedItemTransition[] { new(90, "RM-090") },
            [new RoadmapClosedItemTransition(100, "RM-100")]);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation GitHub snapshot is stale.");
    }

    /// <summary>Validating before write throws that the repository input snapshot is stale when the current input digest differs from the plan's, with the head unchanged.</summary>
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
            new RoadmapClosedItemTransition[] { new(90, "RM-090") },
            [new RoadmapClosedItemTransition(100, "RM-100")]);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation repository input snapshot is stale.");
    }

    /// <summary>Validating before write throws that the closure candidates changed after approval when the current required transition is issue 101 but the plan requires issue 100.</summary>
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
            new RoadmapClosedItemTransition[] { new(90, "RM-090") },
            [new RoadmapClosedItemTransition(101, "RM-101")]);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation closure candidates changed after approval.");
    }

    /// <summary>A plan with no required transitions and an unchanged snapshot reports no changes and passes before-write validation against matching current state.</summary>
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
            new RoadmapClosedItemTransition[] { new(90, "RM-090") },
            []);

        // Assert
        plan.HasChanges.ShouldBeFalse();
        plan.RequiredClosedTransitions.ShouldBeEmpty();
    }

    /// <summary>Creating a plan throws that it contains an invalid roadmap item ID when a transition's item id carries a newline and a <c>::warning::</c> workflow command.</summary>
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
            Array.Empty<RoadmapClosedItemTransition>(),
            [new RoadmapClosedItemTransition(100, "RM-100\n::warning::unsafe")],
            snapshotChanged: false);
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation plan contains an invalid roadmap item ID.");
    }

    /// <summary>Adding the approved transition 100 to a manifest that already lists 90 yields exactly 90 and 100, keeps <c>needsHuman</c> intact, and a second add returns identical text.</summary>
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

    /// <summary>Validating before push throws that the closure approvals changed after the protected apply when the current transitions include an extra issue 101.</summary>
    [Fact]
    public void Before_push_validation_rejects_extra_closure_approvals()
    {
        // Arrange
        var plan = RoadmapAutomationTestPlan.Create();
        var currentTransitions = new RoadmapClosedItemTransition[]
        {
            new(90, "RM-090"),
            new(100, "RM-100"),
            new(101, "RM-101")
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

    /// <summary>Validating before push throws the matching message when the plan digest changed, the checkout base is stale, the default branch moved, or the GitHub snapshot digest changed.</summary>
    [Fact]
    public void Before_push_validation_rechecks_every_approved_live_anchor()
    {
        // Arrange
        var plan = RoadmapAutomationTestPlan.Create();
        var approvedTransitions = new RoadmapClosedItemTransition[]
        {
            new(90, "RM-090"),
            new(100, "RM-100")
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
        baseException.Message.ShouldBe("Roadmap automation checkout is not the current default-branch head.");
        defaultBranchException.Message.ShouldBe("Roadmap automation checkout is not the current default-branch head.");
        snapshotException.Message.ShouldBe("Roadmap automation GitHub snapshot is stale.");
    }
}
