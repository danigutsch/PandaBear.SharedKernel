using PandaBear.VaultManager.Tool.Review;
using Terminal.Gui.App;
using Terminal.Gui.Input;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>
/// Runs the review headlessly through <see cref="ReviewApp"/> with the real dialogs, presses a key that opens a dialog, and
/// cancels the review while the dialog is open, as Ctrl+C or SIGTERM cancels the command.
/// </summary>
internal static class CanceledReview
{
    public static CanceledReviewRun Run(ReviewSession session, Key key)
    {
        using IApplication app = HeadlessTerminal.Start();
        using var cancellation = new CancellationTokenSource();
        bool pressed = false;
        string? title = null;
        bool closed = false;

        // A dialog runs its own loop inside the window's loop, so this handler runs again while the dialog is open.
        app.Iteration += (_, _) =>
        {
            if (!pressed)
            {
                pressed = true;
                HeadlessTerminal.Press(app, key);
                return;
            }

            if (cancellation.IsCancellationRequested || (app.SessionStack?.Count ?? 0) < 2 || app.TopRunnable is not { } dialog)
            {
                return;
            }

            title = app.TopRunnableView?.Title;
            cancellation.Cancel();
            closed = dialog.StopRequested;
            if (!closed)
            {
                // The cancellation left the dialog open. Close it and end the review after the next iteration, so the
                // test fails instead of waiting forever.
                app.RequestStop(dialog);
                app.StopAfterFirstIteration = true;
            }
        };

        int exitCode = ReviewApp.Run(app, session, new ReviewDialogs(app), cancellation.Token);
        return new CanceledReviewRun(exitCode, title, closed, app.SessionStack?.Count ?? 0);
    }
}
