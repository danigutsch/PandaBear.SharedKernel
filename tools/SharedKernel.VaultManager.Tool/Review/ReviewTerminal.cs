namespace PandaBear.VaultManager.Tool.Review;

/// <summary>The terminal the review UI runs in.</summary>
/// <param name="OutputIsTerminal">Whether standard output is a terminal.</param>
/// <param name="Run">
/// Runs the UI for a session until the user quits or the token is canceled, and returns the exit code. The terminal is
/// restored before it returns.
/// </param>
internal sealed record ReviewTerminal(Func<bool> OutputIsTerminal, Func<ReviewSession, CancellationToken, int> Run)
{
    /// <summary>The terminal of the current process, with the Terminal.Gui review window.</summary>
    public static ReviewTerminal Console { get; } = new(static () => !System.Console.IsOutputRedirected, ReviewApp.Run);
}
