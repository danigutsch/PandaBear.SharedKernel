namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>
/// Exit codes of the <c>sharedkernel-vault</c> commands. System.CommandLine also exits with
/// <see cref="UnexpectedError"/> for an invalid command line and for a canceled command.
/// </summary>
internal static class ExitCodes
{
    /// <summary>The command succeeded.</summary>
    public const int Success = 0;

    /// <summary>An unexpected error.</summary>
    public const int UnexpectedError = 1;

    /// <summary>Fields or findings need a decision, so the merged vault was not written.</summary>
    public const int DecisionsNeeded = 2;

    /// <summary>
    /// An input or output problem: an unreadable, unsupported, or account-restricted input, a wrong password, a decisions
    /// file that is not valid, an output that exists or is an input, a missing output directory, a report or decisions
    /// file that cannot be written, writer errors that block writing, or a password needed without a terminal. A merge
    /// that writes the vault and then cannot write the report also exits with it, after naming the written vault.
    /// </summary>
    public const int InputOutputProblem = 3;
}
