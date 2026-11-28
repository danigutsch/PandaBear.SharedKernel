using static SharedKernel.Packages.Tool.Tests.ReviewToolHarness;

namespace SharedKernel.Packages.Tool.Tests;

/// <summary>Exercises the review command with fake package readers and terminal ports.</summary>
public sealed class ReviewCommandTests
{
    private const string CargoConfiguration = """{"version":1,"packages":{"cargo":["bat","fd-find","ripgrep=14.1.0"]},"minimumReleaseAgeDays":7}""";

    private static readonly PackageReviewTerminal Redirected =
        new(static () => false, static (_, _) => throw new InvalidOperationException("Review must leave redirected streams alone."));

    [Fact]
    public void The_tool_accepts_review_and_explicit_network_consent()
    {
        // Arrange
        var command = PackagesCommandLine.Create(Path.GetTempPath(), new ExecutableMap(PackagesToolHarness.ResolveAll),
            UpdatesToolHarness.Runner());

        // Act
        var parsed = command.Parse(["review", "--root", ".", "--allow-network", "--minimum-release-age-days", "7"]);

        // Assert
        parsed.Errors.ShouldBeEmpty();
        parsed.CommandResult.Command.Name.ShouldBe("review");
    }

    [Fact]
    public async Task A_redirected_review_reuses_plan_output_without_network_access()
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository("""{"version":1,"packages":{"cargo":["bat","missing"]}}""");

        // Act
        var fallback = await Run(repository.Path, UpdatesToolHarness.Runner(), Redirected, RefuseNetwork, "review");
        var plan = await Run(repository.Path, UpdatesToolHarness.Runner(), Redirected, RefuseNetwork, "plan");

        // Assert
        fallback.ShouldBe(plan);
        fallback.ExitCode.ShouldBe(1);
        fallback.Output.ShouldContain("cargo: install missing", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_redirected_network_review_reuses_update_output_and_exit_codes()
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository(CargoConfiguration);
        var sources = UpdatesToolHarness.Sources();

        // Act
        var fallback = await Run(repository.Path, UpdatesToolHarness.Runner(), Redirected, sources.ToSources, "review", "--allow-network");
        var updates = await Run(repository.Path, UpdatesToolHarness.Runner(), Redirected, sources.ToSources, "updates", "--allow-network");

        // Assert
        fallback.ShouldBe(updates);
        fallback.ExitCode.ShouldBe(0);
        fallback.Output.ShouldContain("Hold fd-find", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_interactive_review_keeps_the_declaration_and_skips_update_sources_by_default()
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository(CargoConfiguration);
        var runner = UpdatesToolHarness.Runner();
        PackageReviewSession? session = null;
        var terminal = new PackageReviewTerminal(static () => true, (report, _) =>
        {
            session = report;
            return report.ExitCode;
        });

        // Act
        var result = await Run(repository.Path, runner, terminal, RefuseNetwork, "review");
        var contents = await File.ReadAllTextAsync(Path.Combine(repository.Path, PackagesConfiguration.RelativePath), TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        var observation = session.ShouldNotBeNull();
        observation.Updates.ShouldBeNull();
        observation.DetailsFor(PackageManagerKind.Cargo).ShouldContain("ripgrep=14.1.0: installed as declared");
        runner.Runs.Select(static run => run.FileName).ShouldBe(["/fake/cargo"]);
        contents.ShouldBe(CargoConfiguration);
    }

    [Fact]
    public async Task A_network_review_shows_the_policy_decisions_without_writing_packages()
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository(CargoConfiguration);
        var runner = UpdatesToolHarness.Runner();
        var sources = UpdatesToolHarness.Sources();
        PackageReviewSession? session = null;
        var terminal = new PackageReviewTerminal(static () => true, (report, _) =>
        {
            session = report;
            return report.ExitCode;
        });

        // Act
        var result = await Run(repository.Path, runner, terminal, sources.ToSources, "review", "--allow-network");

        // Assert
        result.ExitCode.ShouldBe(0);
        var details = string.Join('\n', session.ShouldNotBeNull().DetailsFor(PackageManagerKind.Cargo));
        details.ShouldContain("Allow: bat 0.24.0 -> 0.26.1", StringComparison.Ordinal);
        details.ShouldContain("Hold: fd-find 9.0.0 -> 10.0.0", StringComparison.Ordinal);
        details.ShouldContain("The candidate is too new", StringComparison.Ordinal);
        details.ShouldContain("ripgrep=14.1.0: pinned.", StringComparison.Ordinal);
        sources.Lookups.ShouldBeGreaterThan(0);
        runner.Runs.Select(static run => run.FileName).ShouldBe(["/fake/cargo", "/fake/cargo"]);
    }

    [Fact]
    public async Task Missing_release_age_stops_review_before_invoking_any_ports()
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository("""{"version":1,"packages":{"cargo":["bat"]}}""");
        var runner = UpdatesToolHarness.Runner();
        var terminal = new PackageReviewTerminal(static () => true, static (_, _) => throw new InvalidOperationException("Review needs a validated release age."));

        // Act
        var result = await Run(repository.Path, runner, terminal, RefuseNetwork, "review", "--allow-network");

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Give --minimum-release-age-days or set minimumReleaseAgeDays", StringComparison.Ordinal);
        runner.Runs.ShouldBeEmpty();
    }
}
