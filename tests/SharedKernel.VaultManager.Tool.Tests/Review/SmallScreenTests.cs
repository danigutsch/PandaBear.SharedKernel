using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Review;
using Terminal.Gui.App;
using static PandaBear.VaultManager.Tool.Tests.Review.ReviewVaults;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>
/// Runs the review window with the real dialogs on a terminal smaller than the dialogs need, such as one that reports a
/// size of 0x0, where Terminal.Gui cannot lay a dialog out.
/// </summary>
[Collection(HeadlessTerminal.Collection)]
public sealed class SmallScreenTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("vault-review-small-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void Writing_with_pending_decisions_on_a_zero_size_terminal_shows_the_problem_on_the_message_line()
    {
        // Arrange
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(Path.Combine(directory, "merged.json"))));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.Name, KeeperKey);
        string blocker = session.WriteBlocker.ShouldNotBeNull();
        using IApplication app = HeadlessTerminal.Start();
        app.Driver!.SetScreenSize(0, 0);
        using var window = new ReviewWindow(session, new ReviewDialogs(app));
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Write.Key);
        bool running = window.IsRunning;
        string message = window.Message;
        window.Stop();
        app.End(token);

        // Assert
        running.ShouldBeTrue();
        message.ShouldBe(blocker);
        session.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Writing_a_password_protected_vault_on_a_zero_size_terminal_writes_nothing_and_says_why()
    {
        // Arrange
        string output = Path.Combine(directory, "merged.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output)));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        using IApplication app = HeadlessTerminal.Start();
        app.Driver!.SetScreenSize(0, 0);
        using var window = new ReviewWindow(session, new ReviewDialogs(app));
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // A password dialog that opens anyway runs its own loop; closing it lets the test fail instead of waiting forever.
        app.Iteration += (_, _) => app.RequestStop(app.TopRunnable);

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Write.Key);
        string message = window.Message;
        window.Stop();
        app.End(token);

        // Assert
        message.ShouldContain("too small", StringComparison.Ordinal);
        message.ShouldContain("Nothing was written.", StringComparison.Ordinal);
        session.Written.ShouldBeFalse();
        File.Exists(output).ShouldBeFalse();
    }

    [Fact]
    public void Quitting_with_unsaved_decisions_on_a_zero_size_terminal_keeps_the_review_open_and_save_still_works()
    {
        // Arrange
        string decisionsPath = Path.Combine(directory, "decisions.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(decisionsPath, null));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);
        using IApplication app = HeadlessTerminal.Start();
        app.Driver!.SetScreenSize(0, 0);
        using var window = new ReviewWindow(session, new ReviewDialogs(app));
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Quit.Key);
        app.End(token);
        bool keptOpen = window.IsRunning;
        string message = window.Message;
        HeadlessTerminal.Press(app, ReviewKeys.Save.Key, ReviewKeys.Quit.Key);
        app.End(token);

        // Assert
        keptOpen.ShouldBeTrue();
        message.ShouldContain("too small", StringComparison.Ordinal);
        message.ShouldContain("stays open", StringComparison.Ordinal);
        window.IsRunning.ShouldBeFalse();
        session.IsDirty.ShouldBeFalse();
        File.Exists(decisionsPath).ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(ReviewDialogs.MinimumWidth - 1, ReviewDialogs.MinimumHeight)]
    [InlineData(ReviewDialogs.MinimumWidth, ReviewDialogs.MinimumHeight - 1)]
    public void The_key_list_on_a_terminal_below_the_minimum_size_names_the_minimum_on_the_message_line(int width, int height)
    {
        // Arrange
        var session = Session(ConflictingLogins(directory));
        using IApplication app = HeadlessTerminal.Start();
        app.Driver!.SetScreenSize(width, height);
        using var window = new ReviewWindow(session, new ReviewDialogs(app));
        SessionToken token = app.Begin(window).ShouldNotBeNull();

        // A key list that opens anyway runs its own loop; closing it lets the test fail instead of waiting forever.
        app.Iteration += (_, _) => app.RequestStop(app.TopRunnable);

        // Act
        HeadlessTerminal.Press(app, ReviewKeys.Help.Key);
        string message = window.Message;
        bool running = window.IsRunning;
        app.End(token);

        // Assert
        running.ShouldBeTrue();
        message.ShouldContain("too small", StringComparison.Ordinal);
        message.ShouldContain(ReviewDialogs.MinimumWidth + " columns", StringComparison.Ordinal);
        message.ShouldContain(ReviewDialogs.MinimumHeight + " rows", StringComparison.Ordinal);
    }

    [Fact]
    public void Every_dialog_opens_on_a_terminal_of_the_minimum_size()
    {
        // Arrange
        using IApplication app = HeadlessTerminal.Start();
        app.Driver!.SetScreenSize(ReviewDialogs.MinimumWidth, ReviewDialogs.MinimumHeight);
        var titles = new List<string?>();
        app.Iteration += (_, _) =>
        {
            if (app.TopRunnable is { } dialog && !dialog.StopRequested)
            {
                titles.Add(app.TopRunnableView?.Title);
                app.RequestStop(dialog);
            }
        };
        var dialogs = new ReviewDialogs(app);

        // Act
        var unavailable = dialogs.Unavailable;
        dialogs.AskQuit(canSave: true);
        dialogs.AskQuit(canSave: false);
        dialogs.AskExportPassword();
        dialogs.ShowHelp(ReviewKeys.All);
        dialogs.ShowError(new string('x', 500));

        // Assert
        unavailable.ShouldBeNull();
        titles.ShouldBe(["Unsaved decisions", "Unsaved decisions", "Password for the new export", "Keys", "Problem"]);
    }
}
