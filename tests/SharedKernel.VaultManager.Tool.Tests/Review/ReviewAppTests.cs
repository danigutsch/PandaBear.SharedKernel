using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Review;
using static PandaBear.VaultManager.Tool.Tests.Review.ReviewVaults;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>Runs the review as the command does, with the real dialogs, and cancels it while a dialog is open.</summary>
[Collection(HeadlessTerminal.Collection)]
public sealed class ReviewAppTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("vault-review-app-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);

    /// <summary>Canceling the review while the key list is open closes the list, leaves no open session, and exits with code 1.</summary>
    [Fact]
    public void Canceling_while_the_key_list_is_open_closes_it_and_ends_the_review_with_exit_code_1()
    {
        // Arrange
        var session = Session(ConflictingLogins(directory));

        // Act
        var run = CanceledReview.Run(session, ReviewKeys.Help.Key);

        // Assert
        run.DialogTitle.ShouldBe("Keys");
        run.DialogClosedByCancellation.ShouldBeTrue();
        run.ExitCode.ShouldBe(1);
        run.OpenSessions.ShouldBe(0);
    }

    /// <summary>Canceling the review while a write problem is shown closes the problem dialog, leaves no open session, and exits with code 1.</summary>
    [Fact]
    public void Canceling_while_a_problem_is_shown_closes_it_and_ends_the_review_with_exit_code_1()
    {
        // Arrange
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(Path.Combine(directory, "merged.json"))));

        // Act
        var run = CanceledReview.Run(session, ReviewKeys.Write.Key);

        // Assert
        run.DialogTitle.ShouldBe("Problem");
        run.DialogClosedByCancellation.ShouldBeTrue();
        run.ExitCode.ShouldBe(1);
        run.OpenSessions.ShouldBe(0);
    }

    /// <summary>Canceling the review while the quit question is open exits with code 1 and leaves the decisions dirty and unsaved.</summary>
    [Fact]
    public void Canceling_while_the_quit_question_is_open_ends_the_review_without_saving()
    {
        // Arrange
        string decisionsPath = Path.Combine(directory, "decisions.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(decisionsPath, null));
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

    /// <summary>Canceling the review while the export password is requested exits with code 1 and writes no export.</summary>
    [Fact]
    public void Canceling_while_the_export_password_is_asked_writes_nothing()
    {
        // Arrange
        string output = Path.Combine(directory, "merged.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output)));
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
