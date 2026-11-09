using static PandaBear.Packages.Tool.Tests.PackagesToolHarness;

namespace PandaBear.Packages.Tool.Tests;

/// <summary>Tests the <c>plan</c> command.</summary>
[Trait("Capability", "Packages")]
public sealed class PlanCommandTests
{
    private const string Dpkg = "ii \n";

    [Fact]
    public async Task Missing_and_differing_packages_are_listed_and_the_exit_code_is_one()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"apt":["git","libreoffice"],"cargo":["ripgrep=14.1.0","bat"]}}""");
        var runner = new RoutingProcessRunner(new Dictionary<string, ProcessOutput>
        {
            ["/fake/dpkg-query"] = new(0, "git\t1:2.43\t" + Dpkg),
            ["/fake/cargo"] = new(0, "bat v0.26.1:\n    bat\nripgrep v13.0.0:\n    rg\n"),
        });

        // Act
        var (exitCode, output, error) = await Run(repository.Path, runner, ResolveAll);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldBe(string.Empty);
        output.ShouldBe("apt: install libreoffice\ncargo: change ripgrep from 13.0.0 to 14.1.0\n2 of 4 declared packages need a change.\n".ReplaceLineEndings());
    }

    [Fact]
    public async Task When_everything_is_installed_the_exit_code_is_zero()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"apt":["git"],"flatpak":["org.telegram.desktop"]}}""");
        var runner = new RoutingProcessRunner(new Dictionary<string, ProcessOutput>
        {
            ["/fake/dpkg-query"] = new(0, "git\t1:2.43\t" + Dpkg),
            ["/fake/flatpak"] = new(0, "org.telegram.desktop\t7.0.9\tstable\n"),
        });

        // Act
        var (exitCode, output, error) = await Run(repository.Path, runner, ResolveAll);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldBe("All 2 declared packages are installed.\n".ReplaceLineEndings());
    }

    [Fact]
    public async Task A_package_with_two_owners_is_refused_and_no_tool_is_run()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"apt":["ripgrep","git"],"cargo":["ripgrep"]}}""");
        var runner = new RoutingProcessRunner([]);

        // Act
        var (exitCode, output, error) = await Run(repository.Path, runner, ResolveAll);

        // Assert
        exitCode.ShouldBe(2);
        output.ShouldBe(string.Empty);
        error.ShouldContain("ripgrep is declared for more than one package manager: apt, cargo.", StringComparison.Ordinal);
        runner.Calls.ShouldBeEmpty("Nothing is run before the declaration is valid.");
    }

    [Fact]
    public async Task A_missing_tool_fails_before_any_tool_is_run()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"apt":["git"],"cargo":["bat"]}}""");
        var runner = new RoutingProcessRunner([]);

        // Act
        var (exitCode, _, error) = await Run(repository.Path, runner, ResolveWithoutCargo);

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("cargo was not found on PATH", StringComparison.Ordinal);
        runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_manager_without_declared_packages_needs_no_tool()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"apt":["git"],"cargo":[]}}""");
        var runner = new RoutingProcessRunner(new Dictionary<string, ProcessOutput> { ["/fake/dpkg-query"] = new(0, "git\t1\t" + Dpkg) });

        // Act
        var (exitCode, _, error) = await Run(repository.Path, runner, ResolveWithoutCargo);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task A_manager_that_cannot_be_read_fails_the_run_but_the_others_are_still_reported()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"apt":["libreoffice"],"npm":["corepack"]}}""");
        var runner = new RoutingProcessRunner(new Dictionary<string, ProcessOutput>
        {
            ["/fake/dpkg-query"] = new(0, string.Empty),
            ["/fake/npm"] = new(1, string.Empty, "npm ERR! something broke\x1b[0m\nsecond line"),
        });

        // Act
        var (exitCode, output, error) = await Run(repository.Path, runner, ResolveAll);

        // Assert
        exitCode.ShouldBe(2);
        output.ShouldContain("apt: install libreoffice", StringComparison.Ordinal);
        error.ShouldContain("npm: npm failed with exit code 1. npm ERR! something broke", StringComparison.Ordinal);
        error.ShouldNotContain("\x1b", StringComparison.Ordinal);
        error.ShouldNotContain("second line", StringComparison.Ordinal);
        output.ShouldNotContain("declared packages", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_configuration_file_is_an_error()
    {
        // Arrange
        using var repository = CreateRepository(null);

        // Act
        var (exitCode, _, error) = await Run(repository.Path, new RoutingProcessRunner([]), ResolveAll);

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain(".config/sharedkernel-packages.json does not exist.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_missing_tool_is_reported_before_any_tool_is_run()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"apt":["git"],"cargo":["bat"]}}""");
        var runner = new RoutingProcessRunner([]);

        // Act
        var (exitCode, _, error) = await Run(repository.Path, runner, ResolveNone);

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("dpkg-query was not found on PATH", StringComparison.Ordinal);
        error.ShouldContain("cargo was not found on PATH", StringComparison.Ordinal);
        runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Without_a_root_option_the_current_directory_is_used()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"apt":["git"]}}""");
        var runner = new RoutingProcessRunner(new Dictionary<string, ProcessOutput> { ["/fake/dpkg-query"] = new(0, "git\t1\t" + Dpkg) });

        // Act
        var (exitCode, output, _) = await RunWithArguments(repository.Path, runner, ResolveAll, "plan");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("All 1 declared packages are installed.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_relative_root_is_resolved_from_the_working_directory()
    {
        // Arrange
        using var repository = CreateRepository(null);
        var nested = Path.Combine(repository.Path, "nested", ".config");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(nested, "sharedkernel-packages.json"), """{"version":1,"packages":{"apt":["git"]}}""", TestContext.Current.CancellationToken);
        var runner = new RoutingProcessRunner(new Dictionary<string, ProcessOutput> { ["/fake/dpkg-query"] = new(0, "git\t1\t" + Dpkg) });

        // Act
        var (exitCode, _, error) = await RunWithArguments(repository.Path, runner, ResolveAll, "plan", "--root", "nested");

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task A_control_character_in_a_manager_name_never_reaches_the_terminal()
    {
        // Arrange
        using var repository = CreateRepository("""{"version":1,"packages":{"\u001b[2Jpip":[]}}""");

        // Act
        var (exitCode, _, error) = await Run(repository.Path, new RoutingProcessRunner([]), ResolveAll);

        // Assert
        exitCode.ShouldBe(2);
        error.ShouldContain("is not a known package manager", StringComparison.Ordinal);
        error.ShouldNotContain("\u001b", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_configuration_of_exactly_one_mebibyte_is_accepted_and_one_byte_more_is_an_error()
    {
        // Arrange
        const string Json = """{"version":1,"packages":{}}""";
        using var atLimit = CreateRepository(Json + new string(' ', (1024 * 1024) - Json.Length));
        using var over = CreateRepository(Json + new string(' ', (1024 * 1024) - Json.Length + 1));

        // Act
        var accepted = await Run(atLimit.Path, new RoutingProcessRunner([]), ResolveAll);
        var rejected = await Run(over.Path, new RoutingProcessRunner([]), ResolveAll);

        // Assert
        accepted.ExitCode.ShouldBe(0);
        rejected.ExitCode.ShouldBe(2);
        rejected.Error.ShouldContain("larger than 1 MiB", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_configuration_path_that_is_a_directory_is_an_error_not_a_crash()
    {
        // Arrange
        using var repository = CreateRepository(null);
        Directory.CreateDirectory(Path.Combine(repository.Path, ".config", "sharedkernel-packages.json"));

        // Act
        var (exitCode, output, error) = await Run(repository.Path, new RoutingProcessRunner([]), ResolveAll);

        // Assert
        exitCode.ShouldBe(2);
        output.ShouldBe(string.Empty);
        error.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("""{"version":2,"packages":{}}""", "$.version must be 1.")]
    [InlineData("""{"version":1,"packages":{"pip":["x"]}}""", "$.packages.pip is not a known package manager; the managers are: apt, cargo, npm, flatpak.")]
    [InlineData("""{"version":1,"packages":{"apt":null}}""", "$.packages.apt must be a list of package names, not null.")]
    [InlineData("""{"version":1,"packages":{"apt":["git",null]}}""", "$.packages.apt[1] must be a package name, not null.")]
    [InlineData("""{"version":1,"packages":{"apt":["bad name"]}}""", "$.packages.apt[0] must start with a valid package name.")]
    [InlineData("""{"version":1,"packages":{"apt":["git","x=bad version"]}}""", "$.packages.apt[1] must have a valid version after '='.")]
    [InlineData("""{"version":1,"packages":{"apt":["git"]},"extra":1}""", "is not valid at")]
    [InlineData("""{"version":1,"packages":{"apt":["git"],"apt":["curl"]}}""", "is not valid at")]
    [InlineData("""{"version":1}""", "is not valid at")]
    [InlineData("not json", "is not valid at")]
    public async Task An_invalid_configuration_is_reported_with_its_json_path(string configuration, string expected)
    {
        // Arrange
        using var repository = CreateRepository(configuration);
        var runner = new RoutingProcessRunner([]);

        // Act
        var (exitCode, output, error) = await Run(repository.Path, runner, ResolveAll);

        // Assert
        exitCode.ShouldBe(2);
        output.ShouldBe(string.Empty);
        error.ShouldContain(expected, StringComparison.Ordinal);
        runner.Calls.ShouldBeEmpty();
    }
}
