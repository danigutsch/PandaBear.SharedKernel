using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>
/// Password prompts of one command run. Each prompt observes cancellation and shows its text on one line, a prompt
/// that fails, for example without a terminal, is recognized as an input or output problem, and the run can tell
/// whether a prompt waits for the user.
/// </summary>
internal sealed class PasswordPrompts
{
    private readonly VaultCommandEnvironment environment;
    private readonly CancellationToken ct;
    private InvalidOperationException? failure;
    private bool asking;

    /// <summary>Wraps the prompts of an environment for one run.</summary>
    /// <param name="environment">Environment whose prompts ask the user.</param>
    /// <param name="ct">Cancels the run; checked before and after each prompt.</param>
    public PasswordPrompts(VaultCommandEnvironment environment, CancellationToken ct)
    {
        this.environment = environment;
        this.ct = ct;
        Environment = environment with
        {
            ReadPassword = prompt => Ask(environment.ReadPassword, prompt),
            ReadNewPassword = prompt => Ask(environment.ReadNewPassword, prompt),
        };
    }

    /// <summary>The environment with every prompt going through this instance.</summary>
    public VaultCommandEnvironment Environment { get; }

    /// <summary>Whether standard input is a terminal, so a password can be asked.</summary>
    public bool IsInteractive => environment.IsInteractive;

    /// <summary>
    /// Whether a prompt of this run waits for the user's answer. A canceled run writes nothing after such a prompt, so
    /// the command can end without waiting for it.
    /// </summary>
    public bool IsAsking => Volatile.Read(ref asking);

    /// <summary>Whether an exception is the failure of a prompt of this run.</summary>
    /// <param name="exception">Exception.</param>
    /// <returns><see langword="true"/> when a prompt of this run threw it.</returns>
    public bool Failed(Exception exception) => ReferenceEquals(exception, failure);

    private string Ask(Func<string, string> read, string prompt)
    {
        string answer;
        Volatile.Write(ref asking, true);
        try
        {
            // Checked once the prompt counts as waiting, so Ctrl+C either finds the run at a prompt or ends it here.
            ct.ThrowIfCancellationRequested();
            answer = read(CommandText.OneLine(prompt));
        }
        catch (InvalidOperationException exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            Volatile.Write(ref asking, false);
        }

        // Ctrl+C during a prompt ends the run before the answer is used.
        ct.ThrowIfCancellationRequested();
        return answer;
    }
}
