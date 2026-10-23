using System.CommandLine;
using PandaBear.BuildingBlocks;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>Runs the synchronous work of a command for System.CommandLine and maps its failures to exit codes.</summary>
internal static class CommandAction
{
    /// <summary>
    /// Longest wait for canceled work to stop. It is shorter than System.CommandLine's two-second
    /// <see cref="InvocationConfiguration.ProcessTerminationTimeout"/>, so a canceled command still exits 1.
    /// </summary>
    public static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Sets the action of a command. The work gets the parse result, the password prompts of the run, and the
    /// cancellation token, and writes to the invocation's output and error writers.
    /// </summary>
    /// <param name="command">Command.</param>
    /// <param name="environment">Password prompts, terminal state, and clock.</param>
    /// <param name="work">Work that returns the exit code.</param>
    public static void Set(Command command, VaultCommandEnvironment environment, Func<ParseResult, PasswordPrompts, CancellationToken, int> work) =>
        command.SetAction((parseResult, ct) => Run(parseResult, environment, work, ct));

    /// <summary>
    /// Whether an exception reports an input or output problem: a file that cannot be read or written, or content that is
    /// unsupported, malformed, or account-restricted, or whose password is wrong. Their messages name no vault values.
    /// </summary>
    /// <param name="exception">Exception.</param>
    /// <returns><see langword="true"/> for <see cref="IOException"/>, <see cref="InvalidDataException"/>, and <see cref="UnauthorizedAccessException"/>.</returns>
    public static bool IsFileProblem(Exception exception) => exception is IOException or InvalidDataException or UnauthorizedAccessException;

    private static async Task<int> Run(ParseResult parseResult, VaultCommandEnvironment environment, Func<ParseResult, PasswordPrompts, CancellationToken, int> work, CancellationToken ct)
    {
        // The pipeline is synchronous and a password prompt blocks on the terminal, so the work runs on the thread pool.
        // A canceled command exits 1, as System.CommandLine does for a canceled action.
        var prompts = new PasswordPrompts(environment, ct);
        Task<int> running = Task.Run(() => Guard(parseResult, prompts, work, ct), ct);
        try
        {
            return await running.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested && !prompts.IsAsking)
        {
            // Ctrl+C at a prompt ends the command at once: nothing is being written, and the run writes nothing after the
            // prompt. Elsewhere it can arrive while a file is written, so the command waits for the work to stop at its
            // next cancellation check instead of ending the process in the middle of a write, which would leave a hidden
            // temporary file behind. Work that does not stop in time ends with the process.
            await ((Task)running).WaitAsync(StopTimeout, environment.Time, CancellationToken.None).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            throw;
        }
    }

    private static int Guard(ParseResult parseResult, PasswordPrompts prompts, Func<ParseResult, PasswordPrompts, CancellationToken, int> work, CancellationToken ct)
    {
        TextWriter error = parseResult.InvocationConfiguration.Error;
        try
        {
            return work(parseResult, prompts, ct);
        }
        catch (Exception exception) when (IsFileProblem(exception) || prompts.Failed(exception))
        {
            error.WriteLine(CommandText.OneLine(exception.Message));
        }
        catch (Exception exception) when (exception.ShouldHandleAsFailure(ct))
        {
            // System.CommandLine's default handler prints the exception with its message, and a message can quote the
            // text that failed, such as a vault value. Print the type and stack trace only.
            error.WriteLine($"Unexpected error ({exception.GetType().FullName}). Its message is not shown because it can contain vault values.");
            error.WriteLine(exception.StackTrace);
            return ExitCodes.UnexpectedError;
        }

        return ExitCodes.InputOutputProblem;
    }
}
