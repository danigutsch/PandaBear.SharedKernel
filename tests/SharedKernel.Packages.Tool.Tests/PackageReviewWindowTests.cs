using SharedKernel.Terminal.Gui.Testing;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using static SharedKernel.Packages.Tool.Tests.ReviewToolHarness;

namespace SharedKernel.Packages.Tool.Tests;

/// <summary>Checks the compact review window through the shared buffer-only terminal.</summary>
[Collection(HeadlessTerminal.Collection)]
public sealed class PackageReviewWindowTests
{
    [Fact]
    public async Task Selecting_the_next_manager_shows_wrapped_policy_reasons_in_a_compact_terminal()
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository(
            """{"version":1,"packages":{"apt":["git"],"cargo":["fd-find"]},"minimumReleaseAgeDays":7}""");
        var session = await Observe(repository.Path, ScriptedProcessRunner.ByFileName(new Dictionary<string, ProcessOutput>
        {
            ["/fake/dpkg-query"] = new(0, "git\t2.43.0\tii \n"),
            ["/fake/cargo"] = new(0, UpdatesToolHarness.CargoList),
        }), includeUpdates: true);
        using IApplication app = HeadlessTerminal.Start();
        app.Driver.ShouldNotBeNull().SetScreenSize(42, 12);
        using var window = new PackageReviewWindow(session);
        SessionToken token = app.Begin(window).ShouldNotBeNull();
        var firstManager = window.SelectedManager;
        var firstDetails = window.Details;

        // Act
        HeadlessTerminal.Press(app, Key.CursorDown);
        var linesFit = window.WrappedDetails.All(static line => line.Length <= 28);
        app.End(token);

        // Assert
        firstManager.ShouldBe(PackageManagerKind.Apt);
        firstDetails.ShouldContain("git: installed as declared", StringComparison.Ordinal);
        firstDetails.ShouldContain("Updates are unavailable for this manager.", StringComparison.Ordinal);
        window.SelectedManager.ShouldBe(PackageManagerKind.Cargo);
        window.Details.ShouldContain("The candidate is too new", StringComparison.Ordinal);
        window.WrappedDetails.Count.ShouldBeGreaterThan(window.Details.Split('\n').Length);
        linesFit.ShouldBeTrue();
    }

    [Fact]
    public async Task An_unreadable_manager_shows_unknown_state_and_escapes_terminal_control_characters()
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository("""{"version":1,"packages":{"cargo":["bat"]}}""");
        var session = await Observe(repository.Path, ScriptedProcessRunner.ByFileName(new Dictionary<string, ProcessOutput>
        {
            ["/fake/cargo"] = new(2, string.Empty, "tool failure \u001b[31mred"),
        }), includeUpdates: false);
        using IApplication app = HeadlessTerminal.Start();
        using var window = new PackageReviewWindow(session);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        var details = window.Details;
        app.End(token);

        // Assert
        details.ShouldContain("tool failure", StringComparison.Ordinal);
        details.ShouldContain("bat: installed state unknown", StringComparison.Ordinal);
        details.ShouldNotContain("\u001b", StringComparison.Ordinal);
        session.ExitCode.ShouldBe(2);
    }

    [Fact]
    public async Task An_empty_declaration_has_no_selection_or_package_reads()
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository("""{"version":1,"packages":{}}""");
        var runner = ScriptedProcessRunner.ByFileName([]);
        var session = await Observe(repository.Path, runner, includeUpdates: false);
        using IApplication app = HeadlessTerminal.Start();
        using var window = new PackageReviewWindow(session);
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, Key.Esc);
        app.End(token);

        // Assert
        window.SelectedManager.ShouldBeNull();
        window.Details.ShouldBe("No packages are declared.");
        runner.Runs.ShouldBeEmpty();
        window.IsRunning.ShouldBeFalse();
    }
}
