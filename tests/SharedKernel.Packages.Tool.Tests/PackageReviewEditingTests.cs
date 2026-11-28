using SharedKernel.Terminal.Gui.Testing;
using static SharedKernel.Packages.Tool.Tests.ReviewToolHarness;

namespace SharedKernel.Packages.Tool.Tests;

/// <summary>Exercises the interactive package declaration and the explicit save decision.</summary>
[Collection(HeadlessTerminal.Collection)]
public sealed class PackageReviewEditingTests
{
    private const string Configuration =
        """{"version":1,"packages":{"apt":["git"],"cargo":["bat"]},"minimumReleaseAgeDays":7}""";

    [Theory]
    [InlineData(false, "bat")]
    [InlineData(true, "ripgrep=14.1.0")]
    public async Task The_selected_manager_declaration_is_saved_only_after_confirmation(bool confirm, string expectedCargo)
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository(Configuration);
        var configPath = Path.Combine(repository.Path, PackagesConfiguration.RelativePath);
        var runner = AptAndCargo();
        (bool EditorOpened, bool ConfirmationOpened, string? ContentsAtConfirmation) observation = default;
        var terminal = new PackageReviewTerminal(static () => true, (session, _) =>
        {
            observation = Edit(session, configPath, confirm);
            return session.ExitCode;
        });

        // Act
        var result = await Run(repository.Path, runner, terminal, RefuseNetwork, "review");
        var config = await PackagesConfiguration.Read(repository.Path, TestContext.Current.CancellationToken);
        var contents = await File.ReadAllTextAsync(configPath, TestContext.Current.CancellationToken);

        // Assert
        var editorOpened = observation.EditorOpened;
        editorOpened.ShouldBeTrue();
        observation.ConfirmationOpened.ShouldBeTrue();
        observation.ContentsAtConfirmation.ShouldBe(Configuration);
        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
        var apt = config.Packages.For(PackageManagerKind.Apt).Select(static item => item.ToString());
        var cargo = config.Packages.For(PackageManagerKind.Cargo).Select(static item => item.ToString());
        apt.ShouldBe(["git"]);
        cargo.ShouldBe([expectedCargo]);
        var age = config.MinimumReleaseAge.ShouldNotBeNull();
        age.Value.ShouldBe(TimeSpan.FromDays(7));
        var unchanged = string.Equals(contents, Configuration, StringComparison.Ordinal);
        unchanged.ShouldBe(!confirm);
        runner.Runs.Select(static run => run.FileName).ShouldBe(["/fake/dpkg-query", "/fake/cargo"]);
    }

    [Theory]
    [InlineData("bat", 80, "Tab switches panes.")]
    [InlineData("git=2.43.0", 80, "more than one owner")]
    [InlineData("ripgrep", 42, "terminal is too small")]
    public async Task Unchanged_invalid_or_unconfirmable_drafts_leave_the_file_intact(string replacement, int width, string status)
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository(Configuration);
        var path = Path.Combine(repository.Path, PackagesConfiguration.RelativePath);
        (bool DraftKept, bool Reset, bool Confirmation, string Status) observation = default;
        var runner = AptAndCargo();
        var terminal = new PackageReviewTerminal(static () => true, (session, _) =>
        {
            observation = CheckUnconfirmedEdit(session, replacement, width);
            return session.ExitCode;
        });

        // Act
        var result = await Run(repository.Path, runner, terminal, RefuseNetwork, "review");
        var contents = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

        // Assert
        observation.DraftKept.ShouldBeTrue();
        observation.Reset.ShouldBeTrue();
        observation.Confirmation.ShouldBeFalse();
        observation.Status.ShouldContain(status, StringComparison.Ordinal);
        contents.ShouldBe(Configuration);
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task An_unconfigured_manager_can_be_added_through_the_same_confirmed_edit()
    {
        // Arrange
        using var repository = PackagesToolHarness.CreateRepository(Configuration);
        var path = Path.Combine(repository.Path, PackagesConfiguration.RelativePath);
        var runner = AptAndCargo();
        var terminal = new PackageReviewTerminal(static () => true, (session, _) =>
        {
            Edit(session, path, confirm: true, managerOffset: 2, replacement: "typescript");
            return session.ExitCode;
        });

        // Act
        var result = await Run(repository.Path, runner, terminal, RefuseNetwork, "review");
        var config = await PackagesConfiguration.Read(repository.Path, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        var npm = config.Packages.For(PackageManagerKind.Npm).Select(static item => item.ToString());
        npm.ShouldBe(["typescript"]);
        runner.Runs.Select(static run => run.FileName).ShouldBe(["/fake/dpkg-query", "/fake/cargo"]);
    }
}
