using PandaBear.Results;
using static PandaBear.Packages.Tests.PackageFactory;

namespace PandaBear.Packages.Tests;

/// <summary>Tests that the inventory runs the right readers, plans, and judges updates without printing anything.</summary>
[Trait("Capability", "Packages")]
public sealed class PackageInventoryTests
{
    private const string CargoList = "bat v0.24.0:\n    bat\nripgrep v14.1.0:\n    rg\nfd-find v9.0.0:\n    fd\n";

    private static readonly CancellationToken Cancel = TestContext.Current.CancellationToken;

    private static readonly ExecutableMap All = new(static command => "/fake/" + command);

    [Fact]
    public async Task Plan_runs_each_managers_program_and_returns_the_changes_per_manager()
    {
        // Arrange
        var runner = ScriptedProcessRunner.ByFileName(new Dictionary<string, ProcessOutput>
        {
            ["/fake/dpkg-query"] = new(0, "git\t1:2.43\tii \n"),
            ["/fake/cargo"] = new(0, "ripgrep v13.0.0:\n    rg\n"),
        });
        var inventory = new PackageInventory(runner, All);

        // Act
        var report = await inventory.Plan(Declared((PackageManagerKind.Apt, ["git", "vim"]), (PackageManagerKind.Cargo, ["ripgrep=14.1.0"])), Cancel);

        // Assert
        report.IsComplete.ShouldBeTrue();
        report.MissingTools.ShouldBeEmpty();
        report.Managers.Select(static m => m.Manager).ShouldBe([PackageManagerKind.Apt, PackageManagerKind.Cargo]);
        report.Managers[0].Changes.Select(static c => c.ToString()).ShouldBe(["Install vim"]);
        report.Managers[1].Changes.ShouldHaveSingleItem().Kind.ShouldBe(PackageChangeKind.ChangeVersion);
        report.DeclaredCount.ShouldBe(3);
        report.ChangeCount.ShouldBe(2);
        runner.Runs.Select(static run => run.FileName).ShouldBe(["/fake/dpkg-query", "/fake/cargo"]);
    }

    [Fact]
    public async Task Plan_runs_nothing_when_a_program_is_missing_and_names_every_missing_one()
    {
        // Arrange
        var runner = ScriptedProcessRunner.ByFileName([]);
        var inventory = new PackageInventory(runner, new ExecutableMap(static command => command == "flatpak" ? "/fake/flatpak" : null));

        // Act
        var report = await inventory.Plan(Declared((PackageManagerKind.Apt, ["git"]), (PackageManagerKind.Flatpak, ["org.x.App"]), (PackageManagerKind.Brew, ["wget"])), Cancel);

        // Assert
        report.IsComplete.ShouldBeFalse();
        report.Managers.ShouldBeEmpty();
        report.MissingTools.Select(static t => (t.Manager, t.Executable)).ShouldBe([(PackageManagerKind.Apt, "dpkg-query"), (PackageManagerKind.Brew, "brew")]);
        runner.Runs.ShouldBeEmpty();
    }

    [Fact]
    public async Task Plan_reports_an_unreadable_manager_and_still_plans_the_others()
    {
        // Arrange
        var runner = ScriptedProcessRunner.ByFileName(new Dictionary<string, ProcessOutput>
        {
            ["/fake/brew"] = new(0, "wget 1.24.0 1.25.0\n"),
            ["/fake/snap"] = new(0, "Name Version Rev Tracking Publisher Notes\n"),
        });
        var inventory = new PackageInventory(runner, All);

        // Act
        var report = await inventory.Plan(Declared((PackageManagerKind.Brew, ["wget"]), (PackageManagerKind.Snap, ["firefox"])), Cancel);

        // Assert
        report.IsComplete.ShouldBeFalse();
        report.Managers.Select(static m => m.Manager).ShouldBe([PackageManagerKind.Snap, PackageManagerKind.Brew]);
        report.Managers[1].Error.ShouldNotBeNull().ShouldContain("brew cleanup", StringComparison.Ordinal);
        report.Managers[1].Changes.ShouldBeEmpty();
        report.Managers[0].Error.ShouldBeNull();
        report.Managers[0].Changes.Select(static c => c.ToString()).ShouldBe(["Install firefox"]);
    }

    [Fact]
    public async Task Check_updates_skips_pinned_packages_and_judges_the_others()
    {
        // Arrange
        using var sources = CountingSources.Default();
        var inventory = new PackageInventory(ScriptedProcessRunner.ByFileName(new Dictionary<string, ProcessOutput> { ["/fake/cargo"] = new(0, CargoList) }), All);

        // Act
        var report = await inventory.CheckUpdates(Declared((PackageManagerKind.Cargo, ["bat", "fd-find", "ripgrep=14.1.0"])), Age(7), NetworkAccess.Allowed, sources.Create, Cancel);

        // Assert
        report.HasFailure.ShouldBeFalse();
        report.HasSkipped.ShouldBeFalse();
        var cargo = report.Managers.ShouldHaveSingleItem();
        cargo.Pinned.Select(static p => p.ToString()).ShouldBe(["ripgrep=14.1.0"]);
        cargo.Decisions.Select(static d => (d.Candidate.Package.Value, d.Kind)).ShouldBe([("bat", UpdateDecisionKind.Allow), ("fd-find", UpdateDecisionKind.Hold)]);
        report.CheckedCount.ShouldBe(2);
        report.UpdateCount.ShouldBe(2);
        sources.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Check_updates_lists_unsupported_managers_and_creates_no_sources_when_nothing_is_left()
    {
        // Arrange
        using var sources = CountingSources.Default();
        var runner = ScriptedProcessRunner.ByFileName([]);
        var inventory = new PackageInventory(runner, All);

        // Act
        var report = await inventory.CheckUpdates(Declared((PackageManagerKind.Apt, ["git"]), (PackageManagerKind.Flatpak, ["org.x.App"])), Age(7), NetworkAccess.Allowed, sources.Create, Cancel);

        // Assert
        report.Unsupported.ShouldBe([PackageManagerKind.Apt, PackageManagerKind.Flatpak]);
        report.Managers.ShouldBeEmpty();
        report.HasSkipped.ShouldBeTrue();
        report.HasFailure.ShouldBeFalse();
        sources.Created.ShouldBe(0);
        runner.Runs.ShouldBeEmpty();
    }

    [Fact]
    public async Task Check_updates_runs_and_contacts_nothing_when_a_program_is_missing()
    {
        // Arrange
        using var sources = CountingSources.Default();
        var runner = ScriptedProcessRunner.ByFileName([]);
        var inventory = new PackageInventory(runner, new ExecutableMap(static _ => null));

        // Act
        var report = await inventory.CheckUpdates(Declared((PackageManagerKind.Npm, ["corepack"])), Age(7), NetworkAccess.Allowed, sources.Create, Cancel);

        // Assert
        report.HasFailure.ShouldBeTrue();
        report.MissingTools.Select(static t => t.Executable).ShouldBe(["npm"]);
        sources.Created.ShouldBe(0);
        runner.Runs.ShouldBeEmpty();
    }

    [Fact]
    public async Task Check_updates_reports_packages_that_are_not_on_the_registry_as_skipped()
    {
        // Arrange
        using var sources = CountingSources.Default(notFound: ["fd-find"]);
        var inventory = new PackageInventory(ScriptedProcessRunner.ByFileName(new Dictionary<string, ProcessOutput> { ["/fake/cargo"] = new(0, CargoList) }), All);

        // Act
        var report = await inventory.CheckUpdates(Declared((PackageManagerKind.Cargo, ["bat", "fd-find"])), Age(7), NetworkAccess.Allowed, sources.Create, Cancel);

        // Assert
        report.HasSkipped.ShouldBeTrue();
        report.HasFailure.ShouldBeFalse();
        report.Managers.ShouldHaveSingleItem().NotOnRegistry.Select(static n => n.Value).ShouldBe(["fd-find"]);
        report.CheckedCount.ShouldBe(1);
    }

    [Fact]
    public async Task A_failed_lookup_gives_the_manager_an_error_and_no_decisions()
    {
        // Arrange
        using var sources = CountingSources.Default(Result.Unavailable<IReadOnlyList<Advisory>>("OSV is down"));
        var runner = ScriptedProcessRunner.ByFileName(new Dictionary<string, ProcessOutput>
        {
            ["/fake/cargo"] = new(0, CargoList),
            ["/fake/npm"] = new(1, string.Empty, "npm ERR! broken"),
        });
        var inventory = new PackageInventory(runner, All);

        // Act
        var report = await inventory.CheckUpdates(Declared((PackageManagerKind.Cargo, ["bat"]), (PackageManagerKind.Npm, ["corepack"])), Age(7), NetworkAccess.Allowed, sources.Create, Cancel);

        // Assert
        report.HasFailure.ShouldBeTrue();
        report.Managers[0].Error.ShouldBe("OSV is down");
        report.Managers[0].Decisions.ShouldBeEmpty();
        report.Managers[0].CheckedCount.ShouldBe(0);
        report.Managers[1].Error.ShouldNotBeNull();
        sources.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Check_updates_passes_denied_network_access_to_the_lookups_which_refuse()
    {
        // Arrange
        using var sources = CountingSources.Default();
        var inventory = new PackageInventory(ScriptedProcessRunner.ByFileName(new Dictionary<string, ProcessOutput> { ["/fake/cargo"] = new(0, CargoList) }), All);

        // Act
        var report = await inventory.CheckUpdates(Declared((PackageManagerKind.Cargo, ["bat"])), Age(7), NetworkAccess.Denied, sources.Create, Cancel);

        // Assert
        report.HasFailure.ShouldBeTrue();
        report.Managers.ShouldHaveSingleItem().Error.ShouldNotBeNull().ShouldContain("not allowed", StringComparison.Ordinal);
    }

    [Fact]
    public void The_constructor_rejects_null_dependencies()
    {
        // Arrange
        Action noRunner = static () => _ = new PackageInventory(null!, All);
        Action noExecutables = static () => _ = new PackageInventory(ScriptedProcessRunner.ByFileName([]), null!);

        // Act and assert
        noRunner.ShouldThrow<ArgumentNullException>();
        noExecutables.ShouldThrow<ArgumentNullException>();
    }
}
