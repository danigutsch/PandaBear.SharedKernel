using PandaBear.Testing;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Review;
using static PandaBear.VaultManager.Tool.Tests.Review.ReviewVaults;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>Runs the review as the command does, with the real dialogs, and cancels it while a dialog is open.</summary>
[Collection(HeadlessTerminal.Collection)]
public sealed class ReviewAppTests : IDisposable
{
    private readonly TemporaryDirectory temporaryDirectory = new();

    private string DirectoryPath => temporaryDirectory.Path;

    /// <inheritdoc />
    public void Dispose() => temporaryDirectory.Dispose();

    [Fact]
    public void Canceling_while_the_key_list_is_open_closes_it_and_ends_the_review_with_exit_code_1()
    {
        // Arrange
        var session = Session(ConflictingLogins(DirectoryPath));

        // Act
        var run = CanceledReview.Run(session, ReviewKeys.Help.Key);

        // Assert
        run.DialogTitle.ShouldBe("Keys");
        run.DialogClosedByCancellation.ShouldBeTrue();
        run.ExitCode.ShouldBe(1);
        run.OpenSessions.ShouldBe(0);
    }

    [Fact]
    public void Canceling_while_a_problem_is_shown_closes_it_and_ends_the_review_with_exit_code_1()
    {
        // Arrange
        var session = Session(ConflictingLogins(DirectoryPath), files: new ReviewFiles(null, new PipelineOutput(Path.Combine(DirectoryPath, "merged.json"))));

        // Act
        var run = CanceledReview.Run(session, ReviewKeys.Write.Key);

        // Assert
        run.DialogTitle.ShouldBe("Problem");
        run.DialogClosedByCancellation.ShouldBeTrue();
        run.ExitCode.ShouldBe(1);
        run.OpenSessions.ShouldBe(0);
    }

    [Fact]
    public void Canceling_while_the_quit_question_is_open_ends_the_review_without_saving()
    {
        // Arrange
        string decisionsPath = Path.Combine(DirectoryPath, "decisions.json");
        var session = Session(ConflictingLogins(DirectoryPath), files: new ReviewFiles(decisionsPath, null));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);

        // Act
        var run = CanceledReview.Run(session, ReviewKeys.Quit.Key);

        // Assert
        run.DialogTitle.ShouldBe("Unsaved decisions");
        run.DialogClosedByCancellation.ShouldBeTrue();
        run.ExitCode.ShouldBe(1);
        run.OpenSessions.ShouldBe(0);
        session.IsDirty.ShouldBeTrue();
        File.Exists(decisionsPath).ShouldBeFalse();
    }

    [Fact]
    public void Canceling_while_the_export_password_is_asked_writes_nothing()
    {
        // Arrange
        string output = Path.Combine(DirectoryPath, "merged.json");
        var session = Session(ConflictingLogins(DirectoryPath), files: new ReviewFiles(null, new PipelineOutput(output)));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);

        // Act
        var run = CanceledReview.Run(session, ReviewKeys.Write.Key);

        // Assert
        run.DialogTitle.ShouldBe("Password for the new export");
        run.DialogClosedByCancellation.ShouldBeTrue();
        run.ExitCode.ShouldBe(1);
        run.OpenSessions.ShouldBe(0);
        session.Written.ShouldBeFalse();
        File.Exists(output).ShouldBeFalse();
    }
}
