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

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void Canceling_while_the_key_list_is_open_closes_it_and_ends_the_review_with_exit_code_1()
    {
        var session = Session(ConflictingLogins(directory));

        var run = CanceledReview.Run(session, ReviewKeys.Help.Key);

        run.DialogTitle.ShouldBe("Keys");
        run.DialogClosedByCancellation.ShouldBeTrue();
        run.ExitCode.ShouldBe(1);
        run.OpenSessions.ShouldBe(0);
    }

    [Fact]
    public void Canceling_while_a_problem_is_shown_closes_it_and_ends_the_review_with_exit_code_1()
    {
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(Path.Combine(directory, "merged.json"))));

        var run = CanceledReview.Run(session, ReviewKeys.Write.Key);

        run.DialogTitle.ShouldBe("Problem");
        run.DialogClosedByCancellation.ShouldBeTrue();
        run.ExitCode.ShouldBe(1);
        run.OpenSessions.ShouldBe(0);
    }

    [Fact]
    public void Canceling_while_the_quit_question_is_open_ends_the_review_without_saving()
    {
        string decisionsPath = Path.Combine(directory, "decisions.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(decisionsPath, null));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);

        var run = CanceledReview.Run(session, ReviewKeys.Quit.Key);

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
        string output = Path.Combine(directory, "merged.json");
        var session = Session(ConflictingLogins(directory), files: new ReviewFiles(null, new PipelineOutput(output)));
        session.ChooseMember(GroupOf(session, KeeperKey).Key, FieldPaths.LoginPassword, KeeperKey);

        var run = CanceledReview.Run(session, ReviewKeys.Write.Key);

        run.DialogTitle.ShouldBe("Password for the new export");
        run.DialogClosedByCancellation.ShouldBeTrue();
        run.ExitCode.ShouldBe(1);
        run.OpenSessions.ShouldBe(0);
        session.Written.ShouldBeFalse();
        File.Exists(output).ShouldBeFalse();
    }
}
