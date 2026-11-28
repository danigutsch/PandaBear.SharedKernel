using static SharedKernel.Packages.Tests.PackageFactory;

namespace SharedKernel.Packages.Tests;

/// <summary>Exercises the inventory over canned installed-state output for all supported managers.</summary>
[Trait(TestTraitNames.CapabilityName, "Packages")]
public sealed class PackageInventoryFixtureTests
{
    [Fact]
    public async Task The_six_managers_plan_with_pins_using_only_installed_state_commands()
    {
        // Arrange
        var token = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "package-manager-fixture");
        var apt = Path.Combine(directory, "dpkg-query");
        var cargo = Path.Combine(directory, "cargo");
        var npm = Path.Combine(directory, "npm");
        var flatpak = Path.Combine(directory, "flatpak");
        var snap = Path.Combine(directory, "snap");
        var brew = Path.Combine(directory, "brew");
        var runner = ScriptedProcessRunner.ByFileName(new Dictionary<string, ProcessOutput>
        {
            [apt] = new(0, "curl\t8.1\tii \n"),
            [cargo] = new(0, "ripgrep v14.1.1:\n    rg\n"),
            [npm] = new(0, """{"dependencies":{"corepack":{"version":"0.34.0"}}}"""),
            [flatpak] = new(0, "org.example.App\t1.2\tstable\n"),
            [snap] = new(0, "Name Version Rev Tracking Publisher Notes\nfirefox 124.0 20 latest/stable fixture -\n"),
            [brew] = new(0, "wget 1.24.4\n"),
        });
        var inventory = new PackageInventory(runner, new ExecutableMap(command => Path.Combine(directory, command)));
        var checksum = $"sha256:{new string('a', 64)}";
        var pin = PackagePin.Create(Version("1.30"), checksum).ShouldBeSuccess();
        var declared = DeclaredPackages.Create(new Dictionary<PackageManagerKind, IReadOnlyList<PackageRequirement>>
        {
            [PackageManagerKind.Apt] = [Require("curl"), PackageRequirement.Pinned(Name("git"), pin)],
            [PackageManagerKind.Cargo] = [PackageRequirement.Pinned(Name("ripgrep"), pin)],
            [PackageManagerKind.Npm] = [Require("corepack"), PackageRequirement.Pinned(Name("@example/tools"), pin)],
            [PackageManagerKind.Flatpak] = [PackageRequirement.Pinned(Name("org.example.App"), pin)],
            [PackageManagerKind.Snap] = [Require("firefox"), PackageRequirement.Pinned(Name("core24"), pin)],
            [PackageManagerKind.Brew] = [PackageRequirement.Pinned(Name("wget"), pin)],
        }).ShouldBeSuccess();

        // Act
        var plan = await inventory.Plan(declared, token);
        var changes = plan.Managers.SelectMany(static manager => manager.Changes).ToArray();
        var installedVersions = changes.Select(static change => change.InstalledVersion?.Value).ToArray();
        var plannedPins = changes.Select(static change => change.Requirement.Pin);
        var runs = runner.Runs;

        // Assert
        plan.IsComplete.ShouldBeTrue();
        plan.DeclaredCount.ShouldBe(9);
        changes.Select(static change => (change.Requirement.Name.Value, change.Kind)).ShouldBe([
            ("git", PackageChangeKind.Install),
            ("ripgrep", PackageChangeKind.ChangeVersion),
            ("@example/tools", PackageChangeKind.Install),
            ("org.example.App", PackageChangeKind.ChangeVersion),
            ("core24", PackageChangeKind.Install),
            ("wget", PackageChangeKind.ChangeVersion),
        ]);
        installedVersions.ShouldBe([null, "14.1.1", null, "1.2", null, "1.24.4"]);
        plannedPins.ShouldBe([pin, pin, pin, pin, pin, pin]);
        runs.Select(static run => run.FileName).ShouldBe([apt, cargo, npm, flatpak, snap, brew]);
        runs[0].Arguments.ShouldBe(["-W", @"-f=${binary:Package}\t${Version}\t${db:Status-Abbrev}\n"]);
        runs[1].Arguments.ShouldBe(["install", "--list"]);
        runs[2].Arguments.ShouldBe(["ls", "--global", "--depth=0", "--json"]);
        runs[3].Arguments.ShouldBe(["list", "--app", "--columns=application,version,branch"]);
        runs[4].Arguments.ShouldBe(["list"]);
        runs[5].Arguments.ShouldBe(["list", "--versions"]);
    }

    [Fact]
    public void A_matching_listed_version_produces_no_version_change_for_an_artifact_pin()
    {
        // Arrange
        var pin = PackagePin.Create(Version("1.30"), $"sha256:{new string('a', 64)}").ShouldBeSuccess();
        var set = PackageSet.Create([PackageRequirement.Pinned(Name("git"), pin)]).ShouldBeSuccess();

        // Act
        var changes = PackagePlanner.Plan(set.Requirements, [Installed("git", "1.30")]);

        // Assert
        changes.ShouldBeEmpty();
    }
}
