using SharedKernel.VaultManager.Tool.Cli;
using Terminal.Gui.App;

namespace SharedKernel.VaultManager.Tool.Review;

/// <summary>Runs the review window on the terminal of the current process.</summary>
internal static class ReviewApp
{
    /// <summary>
    /// Runs the review window until the user quits or the token is canceled. Canceling closes any open dialog and stops the
    /// window without asking about unsaved decisions. The terminal is restored before this method returns.
    /// </summary>
    /// <param name="workspace">Review state.</param>
    /// <param name="ct">Cancels the review.</param>
    /// <returns><see cref="ExitCodes.Success"/>, or <see cref="ExitCodes.UnexpectedError"/> when the review was canceled.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="workspace"/> is <see langword="null"/>.</exception>
    public static int Run(ReviewWorkspace workspace, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        using IApplication app = Application.Create();
        app.Init();
        return Run(app, workspace, new ReviewDialogs(app), ct);
    }

    /// <summary>Runs the review window on an initialized application until the user quits or the token is canceled.</summary>
    /// <param name="app">Initialized application; the caller disposes it.</param>
    /// <param name="workspace">Review state.</param>
    /// <param name="dialogs">Questions and messages the window shows.</param>
    /// <param name="ct">Cancels the review.</param>
    /// <returns><see cref="ExitCodes.Success"/>, or <see cref="ExitCodes.UnexpectedError"/> when the review was canceled.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/>, <paramref name="workspace"/>, or <paramref name="dialogs"/> is <see langword="null"/>.</exception>
    internal static int Run(IApplication app, ReviewWorkspace workspace, IReviewDialogs dialogs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(dialogs);

        using var window = new ReviewWindow(workspace, dialogs);
        using CancellationTokenRegistration registration = ct.Register(() => app.Invoke(window.Stop));
        if (!ct.IsCancellationRequested)
        {
            app.Run(window, null);
        }

        return ct.IsCancellationRequested ? ExitCodes.UnexpectedError : ExitCodes.Success;
    }
}
