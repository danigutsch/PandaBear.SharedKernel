using Terminal.Gui.App;

namespace SharedKernel.Packages.Tool;

/// <summary>The interactive terminal entry point for package review.</summary>
/// <param name="IsInteractive">Reports whether input and output support interaction.</param>
/// <param name="Run">Runs the review of the observed package state and returns its exit code.</param>
internal sealed record PackageReviewTerminal(Func<bool> IsInteractive, Func<PackageReviewSession, CancellationToken, int> Run)
{
    /// <summary>Uses the current process terminal when both input and output are connected to it.</summary>
    internal static PackageReviewTerminal Console { get; } =
        new(static () => !System.Console.IsInputRedirected && !System.Console.IsOutputRedirected, RunWindow);

    private static int RunWindow(PackageReviewSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using IApplication app = Application.Create();
        app.Init();
        using var window = new PackageReviewWindow(session);
        using CancellationTokenRegistration registration = cancellationToken.Register(() => app.Invoke(window.Close));
        cancellationToken.ThrowIfCancellationRequested();
        app.Run(window, null);
        return cancellationToken.IsCancellationRequested ? PlanCommand.FailureExitCode : session.ExitCode;
    }
}
