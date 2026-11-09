namespace PandaBear.Packages.Tests;

/// <summary>Tests the snap reader against canned and real <c>snap list</c> output.</summary>
[Trait("Capability", "Packages")]
public sealed class SnapPackageReaderTests
{
    private const string Snap = "/usr/bin/snap";
    private const string HeaderLine = "Name      Version    Rev   Tracking       Publisher   Notes\n";

    [Fact]
    public async Task Snap_lists_snaps_skips_the_header_and_includes_base_and_core_snaps()
    {
        // Arrange
        const string output = HeaderLine
            + "bare      1.0        5     latest/stable  canonical** base\n"
            + "core24    20260824   2124  latest/stable  canonical** base\n"
            + "snapd     2.77.1     28254 latest/stable  canonical** snapd\n"
            + "firefox   158.0-2    9057  latest/stable  mozilla**   -\n";
        var runner = new FakeProcessRunner(new ProcessOutput(0, output));
        var reader = new SnapPackageReader(runner, Snap);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
        var packages = result.ShouldBeSuccess();

        // Assert
        packages.Select(static p => p.ToString()).ShouldBe(["bare=1.0", "core24=20260824", "firefox=158.0-2", "snapd=2.77.1"]);
        runner.Executable.ShouldBe(Snap);
        runner.Arguments.ShouldBe(["list"]);
    }

    [Fact]
    public async Task Snap_keeps_calendar_and_prerelease_versions_as_text_and_reads_a_truncated_tracking_column()
    {
        // Arrange
        const string output = HeaderLine
            + "bitwarden 2026.10.3  179   latest/stable  bitwarden** -\n"
            + "tool      1.0-beta   12    1/stable/…    someone     classic,disabled\n"
            + "tool_dev  2025.3     13    latest/edge    someone     -\n";
        var reader = new SnapPackageReader(new FakeProcessRunner(new ProcessOutput(0, output)), Snap);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
        var packages = result.ShouldBeSuccess();

        // Assert
        packages.Select(static p => p.ToString()).ShouldBe(["bitwarden=2026.10.3", "tool=1.0-beta", "tool_dev=2025.3"]);
    }

    [Fact]
    public async Task Snap_with_only_a_header_or_no_output_is_an_empty_list()
    {
        // Arrange
        IPackageReader[] readers =
        [
            new SnapPackageReader(new FakeProcessRunner(new ProcessOutput(0, HeaderLine)), Snap),
            new SnapPackageReader(new FakeProcessRunner(new ProcessOutput(0, string.Empty, "No snaps are installed yet.")), Snap),
        ];

        // Act
        var counts = new List<int>();
        foreach (var reader in readers)
        {
            var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
            counts.Add(result.ShouldBeSuccess().Count);
        }

        // Assert
        counts.ShouldBe([0, 0]);
    }

    [Theory]
    [InlineData("Name Version Rev Tracking Publisher Notes\nfirefox 158.0 9057 latest/stable mozilla**\n")]
    [InlineData("Name Version Rev Tracking Publisher Notes\nfirefox 158.0 9057 latest/stable mozilla** - extra\n")]
    [InlineData("Name Version Rev Tracking Publisher Notes\nfirefox\n")]
    [InlineData("Name Version Rev Tracking Publisher Notes\nbad!name 1 2 latest/stable x -\n")]
    public async Task Snap_reports_a_malformed_line_by_number_and_never_skips_it(string output)
    {
        // Arrange
        var reader = new SnapPackageReader(new FakeProcessRunner(new ProcessOutput(0, output)), Snap);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue("A line that cannot be read must not look like a missing snap.");
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("line 2", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("firefox 158.0 9057 latest/stable mozilla** -\n")]
    [InlineData("Nom Version Rev Tracking Publisher Notes\n")]
    public async Task Snap_without_the_expected_header_is_an_error_instead_of_reading_the_first_line_as_a_snap(string output)
    {
        // Arrange
        var reader = new SnapPackageReader(new FakeProcessRunner(new ProcessOutput(0, output)), Snap);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("line 1", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Snap_listing_a_name_twice_is_an_error()
    {
        // Arrange
        const string output = HeaderLine + "tool 1 1 latest/stable x -\ntool 2 2 latest/stable x -\n";
        var reader = new SnapPackageReader(new FakeProcessRunner(new ProcessOutput(0, output)), Snap);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("tool more than once", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Snap_that_exits_with_an_error_or_cannot_run_is_an_error_even_when_it_printed_a_table()
    {
        // Arrange
        IPackageReader[] readers =
        [
            new SnapPackageReader(new FakeProcessRunner(new ProcessOutput(1, HeaderLine + "tool 1 1 latest/stable x -\n", "error: cannot communicate with server")), Snap),
            new SnapPackageReader(new ThrowingProcessRunner(new InvalidOperationException("Could not start the process.")), Snap),
        ];

        // Act
        var failures = new List<bool>();
        foreach (var reader in readers)
        {
            var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
            failures.Add(result.IsFailure);
        }

        // Assert
        failures.ShouldBe([true, true]);
    }

    [Fact]
    public void A_relative_snap_path_is_rejected_and_a_null_runner_throws()
    {
        // Act
        Action relative = () => _ = new SnapPackageReader(new FakeProcessRunner(new ProcessOutput(0, string.Empty)), "snap");
        Action noRunner = () => _ = new SnapPackageReader(null!, Snap);

        // Assert
        relative.ShouldThrow<ArgumentException>();
        noRunner.ShouldThrow<ArgumentNullException>();
    }

    [Fact(SkipType = typeof(SnapProbe), SkipUnless = nameof(SnapProbe.IsReachable), Skip = "snap is not installed or its daemon is not reachable.")]
    public async Task The_real_snap_lists_every_snap_that_is_mounted_and_each_plans_as_met()
    {
        // Arrange
        var reader = new SnapPackageReader(new SystemProcessRunner(), SnapProbe.Path);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
        var packages = result.ShouldBeSuccess();

        // Assert
        var listed = packages.Select(static p => p.Name.Value).ToArray();
        SnapProbe.MountedSnapNames().Except(listed).ShouldBeEmpty("Every snap with a current mount must be listed.");
        foreach (var package in packages)
        {
            PackagePlanner.Plan([new PackageRequirement(package.Name, package.Version)], packages).ShouldBeEmpty();
        }
    }
}
