namespace SharedKernel.DevContainers.Tests;

/// <summary>Exercises the optional private Podman cgroup verification without a real engine.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class IsolatedPodmanCgroupBudgetTests
{
    private const string SliceId = "Id=budget.slice\n";
    private const string Loaded = "LoadState=loaded\n";
    private const string Active = "ActiveState=active\n";
    private const string Group = "ControlGroup=" + IsolatedPodmanCgroupFixture.Parent + "\n";
    private const string BoundaryFailure = "finite memory and swap limits could not be verified";
    private const string ObservationFailure = "Could not verify live container placement for the private Podman engine.";

    [Theory]
    [InlineData(".slice", false)]
    [InlineData("-.slice", false)]
    [InlineData("budget.scope", false)]
    [InlineData("nested/budget.slice", false)]
    [InlineData(".slice", true)]
    public async Task Invalid_slice_names_are_rejected_before_querying_systemd(string name, bool excessiveLength)
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture();
        var slice = excessiveLength ? new string('a', 256) + name : name;
        var ct = TestContext.Current.CancellationToken;
        Func<Task> run = () => fixture.Resolve(ct, slice);

        // Act
        var failure = await run.ShouldThrow<ArgumentException>();

        // Assert
        failure.ParamName.ShouldBe("slice");
        failure.Message.ShouldContain("must name a user systemd slice", StringComparison.Ordinal);
        fixture.ManagerQueries.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("relative-systemctl")]
    public async Task Unavailable_systemd_executable_fails_before_querying_the_manager(string? executable)
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture { SystemctlExecutable = executable };
        var ct = TestContext.Current.CancellationToken;
        Func<Task> run = () => fixture.Resolve(ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(BoundaryFailure, StringComparison.Ordinal);
        fixture.ManagerQueries.ShouldBe(0);
    }

    [Theory]
    [InlineData(1, "", false)]
    [InlineData(0, "", true)]
    [InlineData(0, "Id=other.slice\n" + Loaded + Active + Group, false)]
    [InlineData(0, SliceId + "LoadState=not-found\n" + Active + Group, false)]
    [InlineData(0, SliceId + Loaded + "ActiveState=inactive\n" + Group, false)]
    [InlineData(0, SliceId + Loaded + Active + Group + Group, false)]
    [InlineData(0, SliceId + Loaded + Active + "ControlGroup=/fixture/other.slice\n", false)]
    public async Task Manager_output_must_identify_one_active_loaded_slice(int exitCode, string output, bool oversized)
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture
        {
            ManagerResult = new(exitCode, oversized ? new string('a', 4097) : output, string.Empty),
        };
        var ct = TestContext.Current.CancellationToken;
        Func<Task> run = () => fixture.Resolve(ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(BoundaryFailure, StringComparison.Ordinal);
        fixture.ManagerQueries.ShouldBe(1);
        fixture.ChildToken.CanBeCanceled.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unavailable_launcher_membership_retains_the_underlying_read_failure(bool permissionDenied)
    {
        // Arrange
        Exception readFailure = permissionDenied ? new UnauthorizedAccessException("membership denied") : new IOException("membership unavailable");
        var fixture = new IsolatedPodmanCgroupFixture { LauncherReadFailure = readFailure };
        var ct = TestContext.Current.CancellationToken;
        Func<Task> run = () => fixture.Resolve(ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(BoundaryFailure, StringComparison.Ordinal);
        failure.InnerException.ShouldBeSameAs(readFailure);
        fixture.ChildToken.CanBeCanceled.ShouldBeFalse();
    }

    [Theory]
    [InlineData(IsolatedPodmanCgroupFixture.Parent + "-sibling/launcher.scope", "16384", "0")]
    [InlineData(IsolatedPodmanCgroupFixture.Launcher, null, "0")]
    [InlineData(IsolatedPodmanCgroupFixture.Launcher, "16384", "max")]
    public async Task Preflight_rejects_an_unrelated_launcher_or_missing_explicit_limit(
        string launcher, string? memory, string swap)
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture
        {
            LauncherCgroup = launcher,
            MaximumMemory = memory,
            MaximumSwap = swap,
        };
        var ct = TestContext.Current.CancellationToken;
        Func<Task> run = () => fixture.Resolve(ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("finite memory and swap limits could not be verified", StringComparison.Ordinal);
        fixture.ManagerQueries.ShouldBe(1);
        fixture.InspectionCount.ShouldBe(0);
        fixture.ChildToken.CanBeCanceled.ShouldBeFalse();
    }

    [Fact]
    public async Task Confirms_both_live_members_and_keeps_the_child_result()
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture { CompleteChildOnConfirmation = true };
        var ct = TestContext.Current.CancellationToken;
        var budget = await fixture.Resolve(ct);

        // Act
        var result = await budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, TextWriter.Null, ct);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe("preserved output");
        fixture.InspectionCount.ShouldBe(2);
        fixture.VerifiedFiles.ShouldBe(1);
    }

    [Theory]
    [InlineData(IsolatedPodmanCgroupFixture.Parent + "-sibling/libpod.scope/container", null, "payload is outside the requested cgroup budget")]
    [InlineData(IsolatedPodmanCgroupFixture.Parent + "/libpod.scope/container", "32768", "Effective memory/swap bytes: 32768/0")]
    public async Task Cancels_the_child_on_confirmed_placement_or_cap_failure(
        string payloadCgroup, string? memoryAfterLaunch, string expectedDiagnostic)
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture
        {
            PayloadCgroup = payloadCgroup,
            MemoryAfterLaunch = memoryAfterLaunch,
        };
        var ct = TestContext.Current.CancellationToken;
        var budget = await fixture.Resolve(ct);
        Func<Task> run = () => budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, TextWriter.Null, ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(expectedDiagnostic, StringComparison.Ordinal);
        fixture.ChildToken.IsCancellationRequested.ShouldBeTrue();
        fixture.InspectionCount.ShouldBe(2);
        fixture.VerifiedFiles.ShouldBe(1);
    }

    [Fact]
    public async Task Rejects_loss_of_the_aggregate_cap_even_when_both_members_retain_caps()
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture
        {
            MemoryAtChildStart = "max",
            MemberLimits = ("4096", "0"),
            CompleteChildOnConfirmation = true,
        };
        var ct = TestContext.Current.CancellationToken;
        var budget = await fixture.Resolve(ct);
        Func<Task> run = () => budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, TextWriter.Null, ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("finite memory and swap limits could not be verified", StringComparison.Ordinal);
        fixture.ChildToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task Rejects_confirmation_from_a_restarted_container_with_reused_process_IDs()
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture
        {
            RestartOnConfirmation = true,
            CompleteChildOnConfirmation = true,
        };
        var ct = TestContext.Current.CancellationToken;
        var budget = await fixture.Resolve(ct);
        Func<Task> run = () => budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, TextWriter.Null, ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("No complete live private Podman payload and conmon pair was observed", StringComparison.Ordinal);
        fixture.InspectionCount.ShouldBe(2);
    }

    [Fact]
    public async Task Cancels_and_joins_the_child_before_propagating_an_unexpected_observer_fault()
    {
        // Arrange
        var original = new FormatException("The private inventory probe faulted.");
        var fixture = new IsolatedPodmanCgroupFixture { InventoryFailure = original };
        var ct = TestContext.Current.CancellationToken;
        var budget = await fixture.Resolve(ct);

        // Act
        var failure = await Record.ExceptionAsync(() =>
            budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, TextWriter.Null, ct)
                .WaitAsync(TimeSpan.FromSeconds(30), ct));
        var canceled = fixture.ChildToken.IsCancellationRequested;
        var completed = fixture.ChildCompleted;
        _ = await Record.ExceptionAsync(fixture.ReleaseChild);

        // Assert
        failure.ShouldBeSameAs(original);
        canceled.ShouldBeTrue();
        completed.ShouldBeTrue();
        fixture.InspectionCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(1, "")]
    [InlineData(0, "cgroupfs\n")]
    public async Task Unsupported_engine_cgroup_manager_is_rejected_before_starting_the_child(int exitCode, string manager)
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture
        {
            QueryOverride = (arguments, _) => arguments[0] == "info" ? new(exitCode, manager, string.Empty) : null,
        };
        var ct = TestContext.Current.CancellationToken;
        var budget = await fixture.Resolve(ct);
        Func<Task> run = () => budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, TextWriter.Null, ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("requires local rootless Podman with the systemd cgroup manager", StringComparison.Ordinal);
        fixture.ChildToken.CanBeCanceled.ShouldBeFalse();
        fixture.VerifiedFiles.ShouldBe(0);
    }

    [Fact]
    public async Task A_nonzero_child_result_keeps_precedence_and_reports_the_observed_placement_failure()
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture
        {
            ImmediateExitCode = 37,
            PayloadCgroup = IsolatedPodmanCgroupFixture.Parent + "-outside/libpod.scope",
        };
        using var error = new InvariantStringWriter();
        var ct = TestContext.Current.CancellationToken;
        var budget = await fixture.Resolve(ct);

        // Act
        var result = await budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, error, ct);

        // Assert
        result.ExitCode.ShouldBe(37);
        result.StandardOutput.ShouldBe("preserved output");
        error.ToString().ShouldContain("payload is outside the requested cgroup budget", StringComparison.Ordinal);
        fixture.InspectionCount.ShouldBe(2);
    }

    [Fact]
    public async Task A_slice_cap_removed_during_confirmation_invalidates_the_observed_pair()
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture
        {
            ImmediateExitCode = 0,
            MemoryAfterLaunch = "max",
            MemberLimits = ("4096", "0"),
        };
        var ct = TestContext.Current.CancellationToken;
        var budget = await fixture.Resolve(ct);
        Func<Task> run = () => budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, TextWriter.Null, ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(BoundaryFailure, StringComparison.Ordinal);
        fixture.InspectionCount.ShouldBe(2);
    }

    [Theory]
    [InlineData(1, "")]
    [InlineData(0, "not-a-container-identity\n")]
    public async Task Invalid_inventory_fails_visibly_without_inspecting_any_processes(int exitCode, string inventory)
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture
        {
            ImmediateExitCode = 0,
            QueryOverride = (arguments, _) => arguments[0] == "ps" ? new(exitCode, inventory, string.Empty) : null,
        };
        var ct = TestContext.Current.CancellationToken;
        var budget = await fixture.Resolve(ct);
        Func<Task> run = () => budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, TextWriter.Null, ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldBe(ObservationFailure);
        fixture.InspectionCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(true, ObservationFailure)]
    [InlineData(false, "No complete live private Podman payload and conmon pair was observed")]
    public async Task Failed_inspection_rechecks_whether_the_container_still_exists(bool stillListed, string diagnostic)
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture
        {
            ImmediateExitCode = 0,
            QueryOverride = (arguments, inspections) => arguments[0] switch
            {
                "container" => new(1, string.Empty, string.Empty),
                "ps" when inspections != 0 && !stillListed => new(0, string.Empty, string.Empty),
                _ => null,
            },
        };
        var ct = TestContext.Current.CancellationToken;
        var budget = await fixture.Resolve(ct);
        Func<Task> run = () => budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, TextWriter.Null, ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(diagnostic, StringComparison.Ordinal);
        fixture.InspectionCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("\ttrue", false)]
    [InlineData("\ttrue\tfalse\t1001\t1002\t2026-11-26T12:00:00Z", true)]
    [InlineData("\tunknown\tfalse\t1001\t1002\t2026-11-26T12:00:00Z", false)]
    [InlineData("\ttrue\tunknown\t1001\t1002\t2026-11-26T12:00:00Z", false)]
    [InlineData("\ttrue\tfalse\t1001\t1002\t", false)]
    public async Task Untrusted_container_inspection_metadata_cannot_establish_identity(string fields, bool wrongIdentity)
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture
        {
            ImmediateExitCode = 0,
            QueryOverride = (arguments, _) => arguments[0] == "container"
                ? new(0, (wrongIdentity ? "other" : arguments[4]) + fields, string.Empty) : null,
        };
        var ct = TestContext.Current.CancellationToken;
        var budget = await fixture.Resolve(ct);
        Func<Task> run = () => budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, TextWriter.Null, ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldBe(ObservationFailure);
        fixture.InspectionCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("unknown", "max", "payload identity or cgroup could not be verified")]
    [InlineData("0", "max", "payload identity or cgroup could not be verified")]
    [InlineData("1001", "invalid", "limits of a live private Podman payload could not be verified")]
    public async Task A_reported_member_requires_a_live_process_and_readable_caps(string pid, string memory, string diagnostic)
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture
        {
            ImmediateExitCode = 0,
            MemberLimits = (memory, "max"),
            QueryOverride = (arguments, _) => arguments[0] == "container"
                ? new(0, arguments[4] + "\ttrue\tfalse\t" + pid + "\t1002\t2026-11-26T12:00:00Z", string.Empty) : null,
        };
        var ct = TestContext.Current.CancellationToken;
        var budget = await fixture.Resolve(ct);
        Func<Task> run = () => budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, TextWriter.Null, ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain(diagnostic, StringComparison.Ordinal);
        fixture.InspectionCount.ShouldBe(2);
    }

    [Fact]
    public async Task A_listed_container_that_has_stopped_cannot_supply_a_live_pair()
    {
        // Arrange
        var fixture = new IsolatedPodmanCgroupFixture
        {
            ImmediateExitCode = 0,
            QueryOverride = static (arguments, _) => arguments[0] == "container"
                ? new(0, arguments[4] + "\tfalse\tfalse\t1001\t1002\t2026-11-26T12:00:00Z", string.Empty) : null,
        };
        var ct = TestContext.Current.CancellationToken;
        var budget = await fixture.Resolve(ct);
        Func<Task> run = () => budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, TextWriter.Null, ct);

        // Act
        var failure = await run.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("No complete live private Podman payload and conmon pair was observed", StringComparison.Ordinal);
        fixture.InspectionCount.ShouldBe(1);
    }

    [Fact]
    public async Task Caller_cancellation_stops_the_observer_and_joins_the_child()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var fixture = new IsolatedPodmanCgroupFixture
        {
            QueryOverride = (arguments, _) =>
            {
                if (arguments[0] == "ps")
                {
                    caller.Cancel();
                    caller.Token.ThrowIfCancellationRequested();
                }

                return null;
            },
        };
        var budget = await fixture.Resolve(ct);
        Func<Task> run = () => budget.Run(fixture.Command, fixture, fixture.VerifyFiles, fixture.Query, TextWriter.Null, caller.Token);

        // Act
        await run.ShouldThrowAssignableTo<OperationCanceledException>();

        // Assert
        fixture.ChildToken.IsCancellationRequested.ShouldBeTrue();
        fixture.ChildCompleted.ShouldBeTrue();
        fixture.InspectionCount.ShouldBe(0);
    }
}
