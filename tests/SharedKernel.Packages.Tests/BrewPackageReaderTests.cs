namespace PandaBear.Packages.Tests;

/// <summary>Tests the Homebrew reader against canned <c>brew list --versions</c> output.</summary>
[Trait("Capability", "Packages")]
public sealed class BrewPackageReaderTests
{
    private const string Brew = "/home/linuxbrew/.linuxbrew/bin/brew";

    [Fact]
    public async Task Brew_lists_each_formula_with_its_version_ordered_by_name()
    {
        // Arrange
        var runner = new FakeProcessRunner(new ProcessOutput(0, "wget 1.25.0\nopenssl@3 3.4.1\nripgrep 14.1.1_1\nopenssl@1.1 1.1.1w\n"));
        var reader = new BrewPackageReader(runner, Brew);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
        var packages = result.ShouldBeSuccess();

        // Assert
        packages.Select(static p => p.ToString()).ShouldBe(["openssl@1.1=1.1.1w", "openssl@3=3.4.1", "ripgrep=14.1.1_1", "wget=1.25.0"]);
        runner.Executable.ShouldBe(Brew);
        runner.Arguments.ShouldBe(["list", "--versions"]);
    }

    [Fact]
    public async Task Brew_with_more_than_one_installed_version_is_an_error_not_a_pick()
    {
        // Arrange
        var reader = new BrewPackageReader(new FakeProcessRunner(new ProcessOutput(0, "wget 1.25.0\nripgrep 13.0.0 14.1.1\n")), Brew);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue("A plan cannot tell which installed version is meant.");
        var detail = result.ErrorDetails.ShouldNotBeNull().Detail;
        detail.ShouldContain("line 2", StringComparison.Ordinal);
        detail.ShouldContain("brew cleanup", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Brew_printing_nothing_is_an_empty_list()
    {
        // Arrange
        var reader = new BrewPackageReader(new FakeProcessRunner(new ProcessOutput(0, string.Empty)), Brew);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
        var packages = result.ShouldBeSuccess();

        // Assert
        packages.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("wget\n")]
    [InlineData("bad!name 1.0\n")]
    [InlineData("wget  1.0\n")]
    [InlineData(" wget 1.0\n")]
    public async Task Brew_reports_a_malformed_line_by_number(string output)
    {
        // Arrange
        var reader = new BrewPackageReader(new FakeProcessRunner(new ProcessOutput(0, output)), Brew);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("line 1", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Brew_listing_a_name_twice_is_an_error()
    {
        // Arrange
        var reader = new BrewPackageReader(new FakeProcessRunner(new ProcessOutput(0, "wget 1.0\nwget 1.0\n")), Brew);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("wget more than once", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Brew_listing_a_formula_and_a_cask_with_one_name_says_so()
    {
        // Arrange
        var reader = new BrewPackageReader(new FakeProcessRunner(new ProcessOutput(0, "docker 1.0\ndocker 4.0\n")), Brew);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("formula and a cask", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Brew_that_exits_with_an_error_or_cannot_run_is_an_error()
    {
        // Arrange
        IPackageReader[] readers =
        [
            new BrewPackageReader(new FakeProcessRunner(new ProcessOutput(1, "wget 1.0\n", "Error: broken")), Brew),
            new BrewPackageReader(new ThrowingProcessRunner(new InvalidOperationException("Could not start the process.")), Brew),
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
    public void A_relative_brew_path_is_rejected_and_a_null_runner_throws()
    {
        // Act
        Action relative = () => _ = new BrewPackageReader(new FakeProcessRunner(new ProcessOutput(0, string.Empty)), "brew");
        Action noRunner = () => _ = new BrewPackageReader(null!, Brew);

        // Assert
        relative.ShouldThrow<ArgumentException>();
        noRunner.ShouldThrow<ArgumentNullException>();
    }
}
