namespace PandaBear.Packages.Tests;

/// <summary>Tests the cargo, npm, and Flatpak readers against canned tool output.</summary>
[Trait("Capability", "Packages")]
public sealed class OtherPackageReaderTests
{
    private const string Cargo = "/home/user/.cargo/bin/cargo";
    private const string Npm = "/usr/bin/npm";
    private const string Flatpak = "/usr/bin/flatpak";

    [Fact]
    public async Task Cargo_lists_each_crate_with_its_version_and_ignores_the_binary_lines()
    {
        // Arrange
        var runner = new FakeProcessRunner(new ProcessOutput(0, "ripgrep v14.1.0:\n    rg\nbat v0.24.0:\n    bat\nfj v0.4.0 (https://example.test/fj#abc123):\n    fj\n"));
        var reader = new CargoPackageReader(runner, Cargo);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
        var packages = result.ShouldBeSuccess();

        // Assert
        packages.Select(static p => p.ToString()).ShouldBe(["bat=0.24.0", "fj=0.4.0", "ripgrep=14.1.0"]);
        runner.Executable.ShouldBe(Cargo);
        runner.Arguments.ShouldBe(["install", "--list"]);
    }

    [Theory]
    [InlineData("ripgrep 14.1.0:\n")]
    [InlineData("bad name v1.0.0:\n")]
    [InlineData("ripgrep v:\n")]
    public async Task Cargo_reports_an_unreadable_crate_line_by_number(string output)
    {
        // Arrange
        var reader = new CargoPackageReader(new FakeProcessRunner(new ProcessOutput(0, output)), Cargo);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("line 1", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Npm_lists_global_packages_including_scoped_ones()
    {
        // Arrange
        const string json = """{"name":"lib","dependencies":{"@anthropic-ai/claude-code":{"version":"2.1.319"},"corepack":{"version":"0.35.0"}}}""";
        var runner = new FakeProcessRunner(new ProcessOutput(0, json));
        var reader = new NpmPackageReader(runner, Npm);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
        var packages = result.ShouldBeSuccess();

        // Assert
        packages.Select(static p => p.ToString()).ShouldBe(["@anthropic-ai/claude-code=2.1.319", "corepack=0.35.0"]);
        runner.Arguments.ShouldBe(["ls", "--global", "--depth=0", "--json"]);
    }

    [Fact]
    public async Task Npm_with_no_dependencies_is_an_empty_list()
    {
        // Arrange
        var reader = new NpmPackageReader(new FakeProcessRunner(new ProcessOutput(0, """{"name":"lib"}""")), Npm);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
        var packages = result.ShouldBeSuccess();

        // Assert
        packages.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"dependencies":[]}""")]
    [InlineData("""{"dependencies":{"corepack":{}}}""")]
    [InlineData("""{"dependencies":{"corepack":{"version":5}}}""")]
    public async Task Npm_reports_unexpected_output_as_an_error(string json)
    {
        // Arrange
        var reader = new NpmPackageReader(new FakeProcessRunner(new ProcessOutput(0, json)), Npm);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue("Unreadable output must not look like an empty system.");
    }

    [Fact]
    public async Task Flatpak_lists_applications_and_uses_the_branch_when_there_is_no_version()
    {
        // Arrange
        var runner = new FakeProcessRunner(new ProcessOutput(0, "org.telegram.desktop\t6.1.4\tstable\nio.podman_desktop.PodmanDesktop\t\tstable\n"));
        var reader = new FlatpakPackageReader(runner, Flatpak);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
        var packages = result.ShouldBeSuccess();

        // Assert
        packages.Select(static p => p.ToString()).ShouldBe(["io.podman_desktop.PodmanDesktop=stable", "org.telegram.desktop=6.1.4"]);
        runner.Arguments.ShouldBe(["list", "--app", "--columns=application,version,branch"]);
    }

    [Theory]
    [InlineData("org.telegram.desktop\t6.1.4\n")]
    [InlineData("bad id\t1\tstable\n")]
    [InlineData("org.example.App\t\t\n")]
    public async Task Flatpak_reports_an_unreadable_line_by_number(string output)
    {
        // Arrange
        var reader = new FlatpakPackageReader(new FakeProcessRunner(new ProcessOutput(0, output)), Flatpak);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("line 1", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Flatpak_listing_the_same_application_in_two_installations_is_an_error()
    {
        // Arrange
        var reader = new FlatpakPackageReader(new FakeProcessRunner(new ProcessOutput(0, "org.telegram.desktop\t7.0.9\tstable\norg.telegram.desktop\t7.0.9\tstable\n")), Flatpak);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue("A plan cannot tell which entry is meant.");
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("org.telegram.desktop more than once", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Npm_output_with_an_error_property_is_an_error_not_an_empty_list()
    {
        // Arrange
        var reader = new NpmPackageReader(new FakeProcessRunner(new ProcessOutput(0, """{"error":{"code":"ELSPROBLEMS"}}""")), Npm);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task A_tool_that_cannot_run_is_an_error_result_for_every_reader()
    {
        // Arrange
        var missing = new ThrowingProcessRunner(new InvalidOperationException("Could not start the process."));
        IPackageReader[] readers = [new CargoPackageReader(missing, Cargo), new NpmPackageReader(missing, Npm), new FlatpakPackageReader(missing, Flatpak)];

        // Act
        var results = new List<bool>();
        foreach (var reader in readers)
        {
            var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
            results.Add(result.IsFailure);
        }

        // Assert
        results.ShouldBe([true, true, true]);
    }

    [Fact]
    public async Task A_failing_tool_is_an_error_for_every_reader()
    {
        // Arrange
        IPackageReader[] readers =
        [
            new CargoPackageReader(new FakeProcessRunner(new ProcessOutput(101, string.Empty)), Cargo),
            new NpmPackageReader(new FakeProcessRunner(new ProcessOutput(1, "{}")), Npm),
            new FlatpakPackageReader(new FakeProcessRunner(new ProcessOutput(1, string.Empty)), Flatpak),
        ];

        // Act
        var results = new List<bool>();
        foreach (var reader in readers)
        {
            var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
            results.Add(result.IsFailure);
        }

        // Assert
        results.ShouldBe([true, true, true]);
    }

    [Fact]
    public void A_relative_tool_path_is_rejected_by_every_reader()
    {
        // Arrange
        var runner = new FakeProcessRunner(new ProcessOutput(0, string.Empty));

        // Act
        Action[] constructions =
        [
            () => _ = new CargoPackageReader(runner, "cargo"),
            () => _ = new NpmPackageReader(runner, "npm"),
            () => _ = new FlatpakPackageReader(runner, "flatpak"),
        ];

        // Assert
        foreach (var construct in constructions)
        {
            construct.ShouldThrow<ArgumentException>();
        }
    }
}
