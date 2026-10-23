using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Merge;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>Command environment for tests: records every prompt, answers new-password prompts, and refuses file passwords.</summary>
/// <param name="newPassword">Password returned for a new export.</param>
internal sealed class TestPrompts(string newPassword)
{
    private readonly List<string> filePasswordPrompts = [];
    private readonly List<string> newPasswordPrompts = [];

    public IReadOnlyList<string> FilePasswordPrompts => filePasswordPrompts;

    public IReadOnlyList<string> NewPasswordPrompts => newPasswordPrompts;

    public VaultCommandEnvironment Environment() => new(
        TextWriter.Null,
        TextWriter.Null,
        prompt =>
        {
            filePasswordPrompts.Add(prompt);
            throw new InvalidOperationException("The test inputs are not password-protected.");
        },
        prompt =>
        {
            newPasswordPrompts.Add(prompt);
            return newPassword;
        },
        IsInteractive: true,
        new FixedTimeProvider(PipelineInputs.Now));
}
