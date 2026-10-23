namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>
/// Password prompts, terminal state, and clock shared by the commands and the review UI. Commands write their output
/// through System.CommandLine's invocation configuration.
/// </summary>
/// <param name="ReadPassword">Prompts for an existing password, such as the password of an input file, and returns it.</param>
/// <param name="ReadNewPassword">Prompts for a new password with confirmation, such as the password of an output file, and returns it.</param>
/// <param name="IsInteractive">Whether standard input is a terminal, so prompts and the review UI can run.</param>
/// <param name="Time">Clock.</param>
internal sealed record VaultCommandEnvironment(
    Func<string, string> ReadPassword,
    Func<string, string> ReadNewPassword,
    bool IsInteractive,
    TimeProvider Time)
{
    /// <summary>
    /// Creates the environment of the current process: terminal password prompts written to standard error, and the
    /// system clock. The terminal is opened for a password only at the first prompt, so commands that need no password
    /// run with redirected input; a prompt then throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>The environment.</returns>
    public static VaultCommandEnvironment FromConsole()
    {
        var reader = new Lazy<PasswordReader>(PasswordReader.FromConsole);
        return new VaultCommandEnvironment(
            prompt => reader.Value.Read(prompt),
            prompt => reader.Value.ReadNew(prompt),
            !Console.IsInputRedirected,
            TimeProvider.System);
    }
}
