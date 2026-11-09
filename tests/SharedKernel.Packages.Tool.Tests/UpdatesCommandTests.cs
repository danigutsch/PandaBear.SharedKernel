using PandaBear.Results;
using static PandaBear.Packages.Tool.Tests.PackagesToolHarness;
using static PandaBear.Packages.Tool.Tests.UpdatesToolHarness;

namespace PandaBear.Packages.Tool.Tests;

/// <summary>Tests the <c>updates</c> command.</summary>
[Trait("Capability", "Packages")]
public sealed class UpdatesCommandTests
{
    private const string Config = """{"version":1,"packages":{"cargo":["bat","fd-find","ripgrep=14.1.0"]},"minimumReleaseAgeDays":7}""";

    [Fact]
    public async Task Without_allow_network_the_command_refuses_and_nothing_is_read_or_contacted()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var runner = Runner();
        var created = false;

        // Act
        var (exitCode, output, error) = await Run(repository.Path, runner, ResolveAll, () =>
        {
            created = true;
            return Sources().ToSources();
        });

        // Assert
        exitCode.ShouldBe(2);
        output.ShouldBe(string.Empty);
        error.ShouldContain("--allow-network", StringComparison.Ordinal);
        error.ShouldContain("Nothing was contacted", StringComparison.Ordinal);
        created.ShouldBeFalse();
        runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Without_allow_network_the_refusal_comes_before_the_configuration_is_read()
    {
        // Arrange
        using var repository = CreateRepository(null);

        // Act
        var (exitCode, _, error) = await Run(repository.Path, Runner(), ResolveAll, () => Sources().ToSources());

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("--allow-network", StringComparison.Ordinal);
        error.ShouldNotContain("does not exist", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_updatable_declared_package_gets_a_line_with_the_decision_and_the_reason()
    {
        // Arrange
        using var repository = CreateRepository(Config);

        // Act
        var (exitCode, output, error) = await Run(repository.Path, Runner(), ResolveAll, () => Sources().ToSources(), "--allow-network");

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        var lines = output.ReplaceLineEndings("\n").TrimEnd().Split('\n');
        lines.ShouldBe(
        [
            "cargo: ripgrep is pinned to 14.1.0; not checked.",
            "cargo: Allow bat 0.24.0 -> 0.26.1: The candidate has been public for 30 days; the minimum is 7 days.",
            "cargo: Hold fd-find 9.0.0 -> 10.0.0: The candidate is too new; it can be installed in 5 days.",
            "2 of 2 checked packages have a newer stable version.",
        ]);
    }

    [Fact]
    public async Task The_flag_overrides_the_configured_age()
    {
        // Arrange
        using var repository = CreateRepository(Config);

        // Act
        var (exitCode, output, _) = await Run(repository.Path, Runner(), ResolveAll, () => Sources().ToSources(), "--allow-network", "--minimum-release-age-days", "1");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("Allow fd-find 9.0.0 -> 10.0.0", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_age_from_the_flag_alone_is_enough()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"cargo":["bat"]}}""");

        // Act
        var (exitCode, output, _) = await Run(repository.Path, Runner(), ResolveAll, () => Sources().ToSources(), "--allow-network", "--minimum-release-age-days", "7");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("Allow bat 0.24.0 -> 0.26.1", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_an_age_from_the_flag_or_the_configuration_the_command_fails_before_any_lookup()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"cargo":["bat"]}}""");
        var sources = Sources();

        // Act
        var (exitCode, output, error) = await Run(repository.Path, Runner(), ResolveAll, () => sources.ToSources(), "--allow-network");

        // Assert
        exitCode.ShouldBe(2);
        output.ShouldBe(string.Empty);
        error.ShouldContain("minimumReleaseAgeDays", StringComparison.Ordinal);
        sources.Lookups.ShouldBe(0);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("3651")]
    public async Task An_age_flag_out_of_range_fails(string days)
    {
        // Arrange
        using var repository = CreateRepository(Config);

        // Act
        var (exitCode, _, error) = await Run(repository.Path, Runner(), ResolveAll, () => Sources().ToSources(), "--allow-network", "--minimum-release-age-days", days);

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("0 to 3650", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"version":1,"packages":{},"minimumReleaseAgeDays":3651}""")]
    [InlineData("""{"version":1,"packages":{},"minimumReleaseAgeDays":-1}""")]
    [InlineData("""{"version":1,"packages":{},"minimumReleaseAgeDays":"7"}""")]
    [InlineData("""{"version":1,"packages":{},"minimumReleaseAgeDays":null}""")]
    [InlineData("""{"version":1,"packages":{},"minimumReleaseAgeDay":7}""")]
    public async Task An_invalid_configured_age_or_a_misspelled_property_is_rejected_strictly(string configuration)
    {
        // Arrange
        using var repository = CreateRepository(configuration);

        // Act
        var (exitCode, _, error) = await Run(repository.Path, Runner(), ResolveAll, () => Sources().ToSources(), "--allow-network");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("sharedkernel-packages.json", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_advisory_lookup_prints_no_decision_and_fails()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var sources = Sources(Result.Unavailable<IReadOnlyList<Advisory>>("OSV is down"));

        // Act
        var (exitCode, output, error) = await Run(repository.Path, Runner(), ResolveAll, () => sources.ToSources(), "--allow-network");

        // Assert
        exitCode.ShouldBe(2);
        output.ShouldNotContain("Allow", StringComparison.Ordinal);
        output.ShouldNotContain("checked packages", StringComparison.Ordinal);
        error.ShouldContain("cargo: OSV is down", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_package_that_is_not_on_the_registry_is_reported_the_others_are_judged_and_the_exit_code_is_two()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"cargo":["bat","gitcrate"]},"minimumReleaseAgeDays":7}""");
        var runner = new RoutingProcessRunner(new Dictionary<string, ProcessOutput> { ["/fake/cargo"] = new(0, "bat v0.24.0:\n    bat\ngitcrate v1.0.0 (https://example.test/g#abc):\n    g\n") });
        var sources = new FakeUpdateSources(new()
        {
            ["bat"] = "0.26.1"
        }, new()
        {
            ["bat"] = Now.AddDays(-30)
        }, NoAdvisories, Now, notFound: ["gitcrate"]);

        // Act
        var (exitCode, output, error) = await Run(repository.Path, runner, ResolveAll, () => sources.ToSources(), "--allow-network");

        // Assert
        exitCode.ShouldBe(2);
        output.ShouldContain("cargo: gitcrate is not on the registry; not checked.", StringComparison.Ordinal);
        output.ShouldContain("cargo: Allow bat 0.24.0 -> 0.26.1", StringComparison.Ordinal);
        output.ShouldContain("1 of 1 checked packages", StringComparison.Ordinal);
        error.ShouldContain("incomplete", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_real_lookup_failure_still_fails_the_whole_manager_even_with_a_not_found_package()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"cargo":["bat","gitcrate","zzz"]},"minimumReleaseAgeDays":7}""");
        var runner = new RoutingProcessRunner(new Dictionary<string, ProcessOutput> { ["/fake/cargo"] = new(0, "bat v0.24.0:\n    bat\ngitcrate v1.0.0:\n    g\nzzz v1.0.0:\n    z\n") });
        var sources = new FakeUpdateSources(new()
        {
            ["bat"] = "0.26.1"
        }, new()
        {
            ["bat"] = Now.AddDays(-30)
        }, NoAdvisories, Now, notFound: ["gitcrate"]);

        // Act
        var (exitCode, output, error) = await Run(repository.Path, runner, ResolveAll, () => sources.ToSources(), "--allow-network");

        // Assert
        exitCode.ShouldBe(2);
        output.ShouldNotContain("Allow", StringComparison.Ordinal);
        error.ShouldContain("cargo: zzz: registry is down", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_latest_lookup_names_the_package_and_fails()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"cargo":["bat","unknown-crate"]},"minimumReleaseAgeDays":7}""");
        var runner = new RoutingProcessRunner(new Dictionary<string, ProcessOutput> { ["/fake/cargo"] = new(0, "bat v0.24.0:\n    bat\nunknown-crate v1.0.0:\n    x\n") });

        // Act
        var (exitCode, _, error) = await Run(repository.Path, runner, ResolveAll, () => Sources().ToSources(), "--allow-network");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("cargo: unknown-crate: registry is down", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_blocked_candidate_and_an_urgent_installed_version_are_reported_with_their_advisories()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"cargo":["bat","fd-find"]},"minimumReleaseAgeDays":7}""");
        var advisories = Result.Ok<IReadOnlyList<Advisory>>(
        [
            Advisory.Create("GHSA-urgent", PackageName.Create("bat").Value, AdvisorySeverity.High, [PackageVersion.Create("0.24.0").Value]).Value,
            Advisory.Create("GHSA-block", PackageName.Create("fd-find").Value, AdvisorySeverity.High, [PackageVersion.Create("10.0.0").Value]).Value,
        ]);

        // Act
        var (exitCode, output, _) = await Run(repository.Path, Runner(), ResolveAll, () => Sources(advisories).ToSources(), "--allow-network");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("cargo: Urgent bat 0.24.0 -> 0.26.1: The installed version is affected by GHSA-urgent", StringComparison.Ordinal);
        output.ShouldContain("cargo: Block fd-find 9.0.0 -> 10.0.0: The candidate is affected by GHSA-block.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_manager_without_a_release_source_is_reported_as_unsupported_and_the_rest_still_runs()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"apt":["git"],"flatpak":["org.telegram.desktop"],"npm":["corepack"]},"minimumReleaseAgeDays":7}""");

        // Act
        var (exitCode, output, error) = await Run(repository.Path, Runner(), ResolveAll, () => Sources().ToSources(), "--allow-network");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("incomplete", StringComparison.Ordinal);
        output.ShouldContain("apt: not supported by updates", StringComparison.Ordinal);
        output.ShouldContain("flatpak: not supported by updates", StringComparison.Ordinal);
        output.ShouldContain("npm: Allow corepack 0.35.0 -> 0.40.0", StringComparison.Ordinal);
    }

    [Fact]
    public async Task When_only_unsupported_managers_are_declared_nothing_is_created_or_contacted()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"apt":["git"]},"minimumReleaseAgeDays":7}""");
        var created = false;

        // Act
        var (exitCode, output, _) = await Run(repository.Path, Runner(), ResolveAll, () =>
        {
            created = true;
            return Sources().ToSources();
        }, "--allow-network");

        // Assert
        exitCode.ShouldBe(2);
        output.ShouldContain("nothing to check", StringComparison.Ordinal);
        created.ShouldBeFalse();
    }

    [Fact]
    public async Task A_missing_tool_fails_before_any_source_is_created()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var created = false;

        // Act
        var (exitCode, _, error) = await Run(repository.Path, Runner(), ResolveNone, () =>
        {
            created = true;
            return Sources().ToSources();
        }, "--allow-network");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("cargo was not found on PATH", StringComparison.Ordinal);
        created.ShouldBeFalse();
    }

    [Fact]
    public async Task An_unreadable_installed_list_fails_and_is_reported()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var runner = new RoutingProcessRunner(new Dictionary<string, ProcessOutput> { ["/fake/cargo"] = new(1, string.Empty) });

        // Act
        var (exitCode, output, error) = await Run(repository.Path, runner, ResolveAll, () => Sources().ToSources(), "--allow-network");

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("cargo:", StringComparison.Ordinal);
        output.ShouldNotContain("checked packages", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_plan_command_still_accepts_a_configuration_with_the_age()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"cargo":["bat"]},"minimumReleaseAgeDays":7}""");
        var runner = new RoutingProcessRunner(new Dictionary<string, ProcessOutput> { ["/fake/cargo"] = new(0, "bat v0.24.0:\n    bat\n") });

        // Act
        var (exitCode, output, _) = await Run(repository.Path, runner, ResolveAll);

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldBe("All 1 declared packages are installed.\n".ReplaceLineEndings());
    }

    [Fact]
    public void The_default_sources_are_created_and_disposed_without_any_request()
    {
        // Act
        using var sources = UpdateSources.CreateDefault();

        // Assert
        sources.Advisories.ShouldBeOfType<OsvAdvisorySource>();
        ReferenceEquals(sources.Latest, sources.ReleaseDates).ShouldBeTrue();
    }
}
