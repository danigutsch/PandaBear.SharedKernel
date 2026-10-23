using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>
/// Password prompts of one command run. Each prompt observes cancellation, and a prompt that fails, for example without
/// a terminal, is recognized as an input or output problem. File passwords are remembered for the run, so a merge that
/// reads its inputs a second time does not ask again.
/// </summary>
internal sealed class PasswordPrompts
{
    private readonly VaultCommandEnvironment environment;
    private readonly CancellationToken ct;
    private readonly Dictionary<string, List<string>> answers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> answered = new(StringComparer.Ordinal);
    private InvalidOperationException? failure;

    /// <summary>Wraps the prompts of an environment for one run.</summary>
    /// <param name="environment">Environment whose prompts ask the user.</param>
    /// <param name="ct">Cancels the run; checked before and after each prompt.</param>
    public PasswordPrompts(VaultCommandEnvironment environment, CancellationToken ct)
    {
        this.environment = environment;
        this.ct = ct;
        Environment = environment with { ReadPassword = ReadPassword, ReadNewPassword = ReadNewPassword };
    }

    /// <summary>The environment with every prompt going through this instance.</summary>
    public VaultCommandEnvironment Environment { get; }

    /// <summary>Whether standard input is a terminal, so a password can be asked.</summary>
    public bool IsInteractive => environment.IsInteractive;

    /// <summary>Whether an exception is the failure of a prompt of this run.</summary>
    /// <param name="exception">Exception.</param>
    /// <returns><see langword="true"/> when a prompt of this run threw it.</returns>
    public bool Failed(Exception exception) => ReferenceEquals(exception, failure);

    /// <summary>Answers the file password prompts that follow with the passwords typed so far, in the order they were typed.</summary>
    public void Replay() => answered.Clear();

    private string ReadPassword(string prompt)
    {
        int position = answered.GetValueOrDefault(prompt);
        answered[prompt] = position + 1;
        if (!answers.TryGetValue(prompt, out List<string>? typed))
        {
            typed = [];
            answers.Add(prompt, typed);
        }

        if (position < typed.Count)
        {
            return typed[position];
        }

        string password = Ask(environment.ReadPassword, prompt);
        typed.Add(password);
        return password;
    }

    private string ReadNewPassword(string prompt) => Ask(environment.ReadNewPassword, prompt);

    private string Ask(Func<string, string> read, string prompt)
    {
        ct.ThrowIfCancellationRequested();
        string answer;
        try
        {
            answer = read(prompt);
        }
        catch (InvalidOperationException exception)
        {
            failure = exception;
            throw;
        }

        // Ctrl+C during a prompt ends the run before the answer is used.
        ct.ThrowIfCancellationRequested();
        return answer;
    }
}
