using static SharedKernel.Packages.Tests.PackageFactory;

namespace SharedKernel.Packages.Tests;

/// <summary>Tests plan and apply convergence entirely through fake observations and execution ports.</summary>
[Trait(TestTraitNames.CapabilityName, "Packages")]
public sealed class PackageConvergenceTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preview_reads_the_plan_without_consulting_the_execution_ports()
    {
        // Arrange
        var fixture = new PackageConvergenceFixture();
        var desired = Declared((PackageManagerKind.Cargo, ["ripgrep"]), (PackageManagerKind.Apt, ["git"]));

        // Act
        var report = await fixture.Engine.Run(desired, ports: fixture.Ports(), cancellationToken: Cancel);

        // Assert
        report.ApplyRequested.ShouldBeFalse();
        report.IsComplete.ShouldBeTrue();
        report.Plan.ChangeCount.ShouldBe(2);
        report.Changes.ShouldBeEmpty();
        report.Remaining.ShouldBeNull();
        fixture.Events.ShouldBe(["read:apt", "read:cargo"]);
    }

    [Fact]
    public async Task Applying_in_manager_and_package_order_leaves_an_empty_second_plan()
    {
        // Arrange
        var fixture = new PackageConvergenceFixture();
        fixture.Install(PackageManagerKind.Apt, "git", "1.0.0");
        fixture.Install(PackageManagerKind.Cargo, "already-met", "1.0.0");
        fixture.Install(PackageManagerKind.Cargo, "undeclared", "3.0.0");
        var desired = Declared((PackageManagerKind.Cargo, ["zed", "already-met", "alpha"]), (PackageManagerKind.Apt, ["git=2.0.0"]));
        var ports = fixture.Ports(approve: static (manager, change) =>
            manager == PackageManagerKind.Apt && change.Requirement == Require("git", "2.0.0"));

        // Act
        var applied = await fixture.Engine.Run(desired, apply: true, ports: ports, cancellationToken: Cancel);
        var secondPlan = await fixture.Engine.Run(desired, cancellationToken: Cancel);

        // Assert
        applied.ApplyRequested.ShouldBeTrue();
        applied.IsComplete.ShouldBeTrue();
        applied.Plan.ChangeCount.ShouldBe(3);
        applied.Changes.Select(static result => (result.Manager, result.Change.Kind, result.Privilege, result.Outcome)).ShouldBe([
            (PackageManagerKind.Apt, PackageChangeKind.ChangeVersion, PackageChangePrivilege.Elevated, PackageChangeOutcome.Applied),
            (PackageManagerKind.Cargo, PackageChangeKind.Install, PackageChangePrivilege.Unprivileged, PackageChangeOutcome.Applied),
            (PackageManagerKind.Cargo, PackageChangeKind.Install, PackageChangePrivilege.Unprivileged, PackageChangeOutcome.Applied),
        ]);
        applied.Remaining.ShouldNotBeNull().ChangeCount.ShouldBe(0);
        secondPlan.Plan.ChangeCount.ShouldBe(0);
        fixture.InstalledVersion(PackageManagerKind.Cargo, "undeclared").ShouldBe("3.0.0");
        fixture.Events.ShouldBe([
            "read:apt", "read:cargo", "classify:apt:git", "approve:apt:git", "elevated:apt:git",
            "classify:cargo:alpha", "apply:cargo:alpha", "classify:cargo:zed", "apply:cargo:zed",
            "read:apt", "read:cargo", "read:apt", "read:cargo",
        ]);
    }

    [Fact]
    public async Task A_partial_failure_is_reported_and_the_retry_applies_only_remaining_changes()
    {
        // Arrange
        var fixture = new PackageConvergenceFixture();
        fixture.FailOnce("alpha");
        var desired = Declared((PackageManagerKind.Cargo, ["zed", "alpha", "mid"]));
        var ports = fixture.Ports();

        // Act
        var first = await fixture.Engine.Run(desired, apply: true, ports: ports, cancellationToken: Cancel);
        var retry = await fixture.Engine.Run(desired, apply: true, ports: ports, cancellationToken: Cancel);

        // Assert
        first.IsComplete.ShouldBeFalse();
        first.Changes.Select(static result => (result.Change.Requirement.Name.Value, result.Outcome)).ShouldBe([
            ("alpha", PackageChangeOutcome.Failed), ("mid", PackageChangeOutcome.Applied), ("zed", PackageChangeOutcome.Applied),
        ]);
        first.Changes[0].Detail.ShouldNotBeNull().ShouldContain("fixture apply failed before mutation", StringComparison.Ordinal);
        first.Remaining.ShouldNotBeNull().Managers.ShouldHaveSingleItem().Changes
            .ShouldHaveSingleItem().Requirement.Name.ShouldBe(Name("alpha"));
        retry.IsComplete.ShouldBeTrue();
        retry.Remaining.ShouldNotBeNull().ChangeCount.ShouldBe(0);
        retry.Changes.ShouldHaveSingleItem().Change.Requirement.Name.ShouldBe(Name("alpha"));
        fixture.Attempts.Select(static change => change.Requirement.Name.Value).ShouldBe(["alpha", "mid", "zed", "alpha"]);
    }

    [Fact]
    public async Task A_retry_after_an_ambiguous_failure_observes_the_completed_change_first()
    {
        // Arrange
        var fixture = new PackageConvergenceFixture();
        fixture.FailOnce("alpha", afterMutation: true);
        var desired = Declared((PackageManagerKind.Cargo, ["zed", "alpha"]));
        var ports = fixture.Ports();

        // Act
        var first = await fixture.Engine.Run(desired, apply: true, ports: ports, cancellationToken: Cancel);
        var retry = await fixture.Engine.Run(desired, apply: true, ports: ports, cancellationToken: Cancel);

        // Assert
        first.IsComplete.ShouldBeFalse();
        first.Changes.Select(static result => result.Outcome).ShouldBe([PackageChangeOutcome.Failed, PackageChangeOutcome.Applied]);
        first.Changes[0].Detail.ShouldBe("fixture apply timed out after mutation");
        first.Remaining.ShouldNotBeNull().ChangeCount.ShouldBe(0);
        retry.IsComplete.ShouldBeTrue();
        retry.Changes.ShouldBeEmpty();
        retry.Plan.ChangeCount.ShouldBe(0);
        retry.Remaining.ShouldBeSameAs(retry.Plan);
        fixture.Attempts.Select(static change => change.Requirement.Name.Value).ShouldBe(["alpha", "zed"]);
    }

    [Fact]
    public async Task A_successful_executor_result_requires_the_expected_observed_state()
    {
        // Arrange
        var fixture = new PackageConvergenceFixture { AcknowledgeWithoutMutation = true };
        var desired = Declared((PackageManagerKind.Cargo, ["alpha=2.0.0"]));

        // Act
        var report = await fixture.Engine.Run(desired, apply: true, ports: fixture.Ports(), cancellationToken: Cancel);

        // Assert
        report.IsComplete.ShouldBeFalse();
        report.Changes.ShouldHaveSingleItem().Outcome.ShouldBe(PackageChangeOutcome.Applied);
        report.Remaining.ShouldNotBeNull().ChangeCount.ShouldBe(1);
    }

    [Fact]
    public async Task An_unreadable_initial_manager_blocks_execution()
    {
        // Arrange
        var fixture = new PackageConvergenceFixture { Unreadable = PackageManagerKind.Apt };
        var desired = Declared((PackageManagerKind.Cargo, ["alpha"]), (PackageManagerKind.Apt, ["git"]));

        // Act
        var report = await fixture.Engine.Run(desired, apply: true, ports: fixture.Ports(), cancellationToken: Cancel);

        // Assert
        report.ApplyRequested.ShouldBeTrue();
        report.IsComplete.ShouldBeFalse();
        report.Plan.IsComplete.ShouldBeFalse();
        report.Plan.Managers[0].Error.ShouldNotBeNull().ShouldContain("fixture read failed", StringComparison.Ordinal);
        report.Plan.Managers[1].Changes.ShouldHaveSingleItem();
        report.Changes.ShouldBeEmpty();
        report.Remaining.ShouldBeNull();
        fixture.Events.ShouldBe(["read:apt", "read:cargo"]);
    }

    [Fact]
    public async Task An_unreadable_final_observation_preserves_the_execution_outcome()
    {
        // Arrange
        var fixture = new PackageConvergenceFixture();
        var desired = Declared((PackageManagerKind.Apt, ["git"]));
        var ports = fixture.Ports(approve: (manager, _) =>
        {
            fixture.Unreadable = manager;
            return true;
        });

        // Act
        var report = await fixture.Engine.Run(desired, apply: true, ports: ports, cancellationToken: Cancel);

        // Assert
        report.IsComplete.ShouldBeFalse();
        report.Changes.ShouldHaveSingleItem().Outcome.ShouldBe(PackageChangeOutcome.Applied);
        report.Remaining.ShouldNotBeNull().IsComplete.ShouldBeFalse();
        fixture.InstalledVersion(PackageManagerKind.Apt, "git").ShouldBe("1.0.0");
    }

    [Theory]
    [InlineData(false, PackageChangeOutcome.ElevationUnavailable)]
    [InlineData(true, PackageChangeOutcome.ElevationDenied)]
    public async Task Elevated_changes_need_both_a_dedicated_port_and_approval(bool withElevation, PackageChangeOutcome expected)
    {
        // Arrange
        var fixture = new PackageConvergenceFixture();
        var desired = Declared((PackageManagerKind.Apt, ["git"]), (PackageManagerKind.Cargo, ["alpha"]));

        // Act
        var report = await fixture.Engine.Run(desired, apply: true, ports: fixture.Ports(withElevation: withElevation),
            cancellationToken: Cancel);

        // Assert
        report.IsComplete.ShouldBeFalse();
        report.Changes[0].Outcome.ShouldBe(expected);
        report.Changes[0].Privilege.ShouldBe(PackageChangePrivilege.Elevated);
        report.Changes[1].Outcome.ShouldBe(PackageChangeOutcome.Applied);
        fixture.Attempts.ShouldHaveSingleItem().Requirement.Name.ShouldBe(Name("alpha"));
    }

    [Fact]
    public async Task Elevation_approval_covers_only_the_change_it_names()
    {
        // Arrange
        var fixture = new PackageConvergenceFixture();
        var desired = Declared((PackageManagerKind.Apt, ["vim", "git"]));
        var ports = fixture.Ports(approve: static (manager, change) =>
            manager == PackageManagerKind.Apt && change.Requirement.Name == Name("git"));

        // Act
        var report = await fixture.Engine.Run(desired, apply: true, ports: ports, cancellationToken: Cancel);

        // Assert
        report.Changes.Select(static result => result.Outcome).ShouldBe([PackageChangeOutcome.Applied, PackageChangeOutcome.ElevationDenied]);
        report.Remaining.ShouldNotBeNull().Managers.ShouldHaveSingleItem().Changes
            .ShouldHaveSingleItem().Requirement.Name.ShouldBe(Name("vim"));
        fixture.Events.ShouldBe([
            "read:apt", "classify:apt:git", "approve:apt:git", "elevated:apt:git", "classify:apt:vim", "approve:apt:vim", "read:apt",
        ]);
    }

    [Fact]
    public async Task An_unknown_privilege_classification_never_reaches_an_executor()
    {
        // Arrange
        var fixture = new PackageConvergenceFixture();
        var desired = Declared((PackageManagerKind.Cargo, ["alpha"]));
        var ports = fixture.Ports(policy: static (_, _) => (PackageChangePrivilege)99);

        // Act
        var report = await fixture.Engine.Run(desired, apply: true, ports: ports, cancellationToken: Cancel);

        // Assert
        report.IsComplete.ShouldBeFalse();
        var result = report.Changes.ShouldHaveSingleItem();
        result.Outcome.ShouldBe(PackageChangeOutcome.Unsupported);
        result.Privilege.ShouldBe(PackageChangePrivilege.Unsupported);
        fixture.Attempts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Explicit_apply_requires_ports_before_observing_state()
    {
        // Arrange
        var fixture = new PackageConvergenceFixture();
        var desired = Declared((PackageManagerKind.Cargo, ["alpha"]));

        Func<Task> apply = () => fixture.Engine.Run(desired, apply: true, cancellationToken: Cancel);

        // Act
        var exception = await apply.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("ports");
        fixture.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancellation_during_approval_prevents_the_elevated_call()
    {
        // Arrange
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var fixture = new PackageConvergenceFixture();
        var desired = Declared((PackageManagerKind.Apt, ["git"]));
        var ports = fixture.Ports(approve: (_, _) =>
        {
            cancellation.Cancel();
            return true;
        });

        // Act
        Func<Task> apply = () => fixture.Engine.Run(desired, apply: true, ports: ports, cancellationToken: cancellation.Token);

        // Assert
        await apply.ShouldThrowAssignableTo<OperationCanceledException>();
        fixture.Events.ShouldBe(["read:apt", "classify:apt:git", "approve:apt:git"]);
    }

    [Fact]
    public async Task The_exact_version_and_checksum_reach_the_executor()
    {
        // Arrange
        var fixture = new PackageConvergenceFixture();
        var pin = PackagePin.Create(Version("2.0.0"), $"sha256:{new string('a', 64)}").Value;
        var desired = DeclaredPackages.Create(new Dictionary<PackageManagerKind, IReadOnlyList<PackageRequirement>>
        {
            [PackageManagerKind.Cargo] = [PackageRequirement.Pinned(Name("alpha"), pin)],
        }).Value;

        // Act
        var report = await fixture.Engine.Run(desired, apply: true, ports: fixture.Ports(), cancellationToken: Cancel);

        // Assert
        report.IsComplete.ShouldBeTrue();
        fixture.Attempts.ShouldHaveSingleItem().Requirement.Pin.ShouldBe(pin);
        fixture.InstalledVersion(PackageManagerKind.Cargo, "alpha").ShouldBe("2.0.0");
    }
}
