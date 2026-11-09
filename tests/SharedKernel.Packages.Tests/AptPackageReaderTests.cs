namespace PandaBear.Packages.Tests;

/// <summary>Tests reading installed packages from <c>dpkg-query</c> output.</summary>
[Trait("Capability", "Packages")]
public sealed class AptPackageReaderTests
{
    private const string DpkgQuery = "/usr/bin/dpkg-query";

    [Fact]
    public async Task Installed_packages_including_held_ones_are_listed_ordered_by_name_and_removed_ones_are_not()
    {
        // Arrange
        var runner = new FakeProcessRunner(new ProcessOutput(0, "zsh\t5.9-6\tii \ngit\t1:2.43.0-1\tii \ndocker-ce\t5:27.3.1-1\thi \nold\t1.0\trc \ngone\t1.0\tun \n"));
        var reader = new AptPackageReader(runner, DpkgQuery);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
        var packages = result.ShouldBeSuccess();

        // Assert
        packages.Select(static p => $"{p.Name}={p.Version}").ShouldBe(["docker-ce=5:27.3.1-1", "git=1:2.43.0-1", "zsh=5.9-6"]);
    }

    [Fact]
    public async Task The_command_runs_from_the_absolute_path_with_one_format_argument_and_no_shell_text()
    {
        // Arrange
        var runner = new FakeProcessRunner(new ProcessOutput(0, string.Empty));
        var reader = new AptPackageReader(runner, DpkgQuery);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
        var packages = result.ShouldBeSuccess();

        // Assert
        packages.ShouldBeEmpty();
        runner.Executable.ShouldBe(DpkgQuery);
        runner.Arguments.ShouldBe(["-W", "-f=${binary:Package}\\t${Version}\\t${db:Status-Abbrev}\\n"]);
    }

    [Fact]
    public async Task A_failing_command_is_an_error_not_an_empty_list()
    {
        // Arrange
        var reader = new AptPackageReader(new FakeProcessRunner(new ProcessOutput(2, string.Empty)), DpkgQuery);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue("A missing list must not look like an empty system.");
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("exit code 2", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("git\t1.0\n")]
    [InlineData("git\t1.0\tii \textra\n")]
    [InlineData("bad name\t1.0\tii \n")]
    [InlineData("git\t\tii \n")]
    public async Task An_unexpected_line_is_an_error_that_names_the_line_number(string output)
    {
        // Arrange
        var reader = new AptPackageReader(new FakeProcessRunner(new ProcessOutput(0, output)), DpkgQuery);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("line 1", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("half\t2.0\tiU ")]
    [InlineData("half\t2.0\tiF ")]
    [InlineData("half\t2.0\tiH ")]
    [InlineData("again\t2.0\tiiR")]
    [InlineData("short\t2.0\tii")]
    public async Task A_partly_installed_package_is_an_error_not_a_missing_package(string line)
    {
        // Arrange
        var reader = new AptPackageReader(new FakeProcessRunner(new ProcessOutput(0, line + "\n")), DpkgQuery);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue("A package that needs repair must be reported, not hidden.");
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("line 1", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_package_listed_twice_is_an_error()
    {
        // Arrange
        var reader = new AptPackageReader(new FakeProcessRunner(new ProcessOutput(0, "git\t1\tii \ngit\t2\tii \n")), DpkgQuery);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("git more than once", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tool_that_runs_too_long_is_an_error_result_not_an_exception()
    {
        // Arrange
        var reader = new AptPackageReader(new ThrowingProcessRunner(new TimeoutException("The process did not finish within two minutes.")), DpkgQuery);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("could not run", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Only_the_first_two_hundred_characters_of_the_tools_error_line_are_kept()
    {
        // Arrange
        var reader = new AptPackageReader(new FakeProcessRunner(new ProcessOutput(2, string.Empty, new string('x', 201))), DpkgQuery);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldEndWith(" " + new string('x', 200), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tool_that_cannot_run_is_an_error_result_not_an_exception()
    {
        // Arrange
        var reader = new AptPackageReader(new ThrowingProcessRunner(new InvalidOperationException("Could not start the process.")), DpkgQuery);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldContain("could not run", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failure_message_carries_the_first_line_of_the_tools_error_output()
    {
        // Arrange
        var reader = new AptPackageReader(new FakeProcessRunner(new ProcessOutput(2, string.Empty, "\n  dpkg-query: error: database locked\x1b[0m\nsecond line\n")), DpkgQuery);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        var detail = result.ErrorDetails.ShouldNotBeNull().Detail;
        detail.ShouldContain("exit code 2. dpkg-query: error: database locked", StringComparison.Ordinal);
        detail.ShouldNotContain("second line", StringComparison.Ordinal);
        detail.ShouldNotContain("\x1b", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_canceled_read_still_throws_so_the_caller_can_stop()
    {
        // Arrange
        var reader = new AptPackageReader(new ThrowingProcessRunner(new OperationCanceledException()), DpkgQuery);

        // Act
        Func<Task> act = () => reader.ListInstalled(TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrow<OperationCanceledException>();
    }

    [Fact]
    public void A_relative_dpkg_query_path_is_rejected()
    {
        // Act
        var act = () => new AptPackageReader(new FakeProcessRunner(new ProcessOutput(0, string.Empty)), "dpkg-query");

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public async Task The_system_runner_rejects_a_relative_executable()
    {
        // Act
        Func<Task> act = () => new SystemProcessRunner().Run("dpkg-query", [], TestContext.Current.CancellationToken);

        // Assert
        await act.ShouldThrow<ArgumentException>();
    }

    [Fact(SkipType = typeof(DpkgProbe), SkipUnless = nameof(DpkgProbe.IsInstalled), Skip = "dpkg-query is not installed.")]
    public async Task The_real_dpkg_query_lists_installed_packages_that_plan_as_met()
    {
        // Arrange
        var reader = new AptPackageReader(new SystemProcessRunner(), DpkgProbe.Path);

        // Act
        var result = await reader.ListInstalled(TestContext.Current.CancellationToken);
        var packages = result.ShouldBeSuccess();

        // Assert
        packages.ShouldNotBeEmpty("A system with dpkg has at least one installed package.");
        var first = packages[0];
        PackagePlanner.Plan([new PackageRequirement(first.Name, first.Version)], packages).ShouldBeEmpty();
    }
}
