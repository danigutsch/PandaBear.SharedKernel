using System.CommandLine;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>Arguments and options that several commands share.</summary>
internal static class CommandSymbols
{
    /// <summary>Creates the argument that takes one or more vault export files.</summary>
    /// <returns>The argument.</returns>
    public static Argument<string[]> Inputs() => new Argument<string[]>("inputs")
    {
        Description = "Vault export files: Keeper JSON, zip, or CSV; Bitwarden JSON, password-protected JSON, CSV, or zip. The format is detected from the content.",
        Arity = ArgumentArity.OneOrMore,
    }.AcceptLegalFilePathsOnly();

    /// <summary>Creates the option that names the Markdown report file.</summary>
    /// <returns>The option.</returns>
    public static Option<string> Report() => new Option<string>("--report")
    {
        Description = "Write the full Markdown report to this file, readable only by you. An existing report is replaced; an input never is.",
        HelpName = "file",
    }.AcceptLegalFilePathsOnly();
}
