using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Merge;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>
/// Command environment for tests: records every prompt and answers new-password prompts. File password prompts are
/// refused unless a file password is given.
/// </summary>
/// <param name="newPassword">Password returned for a new export.</param>
/// <param name="filePassword">Password returned for every password-protected input, or <see langword="null"/> to refuse.</param>
internal sealed class TestPrompts(string newPassword, string? filePassword = null)
{
    private readonly List<string> filePasswordPrompts = [];
    private readonly List<string> newPasswordPrompts = [];

    /// <summary>The prompt texts shown so far for password-protected input files.</summary>
    public IReadOnlyList<string> FilePasswordPrompts => filePasswordPrompts;

    /// <summary>The prompt texts shown so far for the password of a new export.</summary>
    public IReadOnlyList<string> NewPasswordPrompts => newPasswordPrompts;

    /// <summary>Whether the environment reports that standard input is a terminal.</summary>
    public bool IsInteractive { get; init; } = true;

    /// <summary>Runs at each new-password prompt before it is answered, for example to cancel the command or to fail.</summary>
    public Action? OnNewPassword
    {
        get; init;
    }

    /// <summary>Runs at each file password prompt before it is answered, for example to change the input file.</summary>
    public Action? OnFilePassword
    {
        get; init;
    }

    /// <summary>Creates the command environment that records prompts, runs the configured hooks, and uses a clock fixed at <see cref="PipelineInputs.Now"/>.</summary>
    public VaultCommandEnvironment Environment() => new(
        prompt =>
        {
            filePasswordPrompts.Add(prompt);
            OnFilePassword?.Invoke();
            return filePassword ?? throw new InvalidOperationException("The test inputs are not password-protected.");
        },
        prompt =>
        {
            newPasswordPrompts.Add(prompt);
            OnNewPassword?.Invoke();
            return newPassword;
        },
        IsInteractive,
        new FixedTimeProvider(PipelineInputs.Now));
}
