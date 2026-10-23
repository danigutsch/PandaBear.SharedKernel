using System.CommandLine;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>Arguments and options that several commands share.</summary>
internal static class CommandSymbols
{
    /// <summary>Creates the argument that takes one or more vault export files.</summary>
    /// <returns>The argument.</returns>
    public static Argument<string[]> Inputs()
    {
        Argument<string[]> inputs = new Argument<string[]>("inputs")
        {
            Description = "Vault export files: Keeper JSON, zip, or CSV; Bitwarden JSON, password-protected JSON, CSV, or zip. The format is detected from the content.",
            Arity = ArgumentArity.OneOrMore,
        }.AcceptLegalFilePathsOnly();
        inputs.Validators.Add(static result =>
        {
            if (result.Tokens.Any(token => string.IsNullOrWhiteSpace(token.Value)))
            {
                result.AddError("Each input needs a file path, not an empty or blank value.");
            }
        });
        return inputs;
    }

    /// <summary>Creates the option that names the Markdown report file.</summary>
    /// <returns>The option.</returns>
    public static Option<string> Report() => FilePath(new Option<string>("--report")
    {
        Description = "Write the full Markdown report to this file, readable only by you. An existing report is replaced; an input never is.",
        HelpName = "file",
    });

    /// <summary>
    /// Makes an option take a file path only: a legal path that is not empty or blank. Both are rejected while parsing,
    /// before the command runs.
    /// </summary>
    /// <param name="option">Option that names a file.</param>
    /// <returns>The same option.</returns>
    public static Option<string> FilePath(Option<string> option)
    {
        ArgumentNullException.ThrowIfNull(option);

        option.AcceptLegalFilePathsOnly();
        option.Validators.Add(result =>
        {
            if (result.Tokens.Any(token => string.IsNullOrWhiteSpace(token.Value)))
            {
                result.AddError($"{option.Name} needs a file path, not an empty or blank value.");
            }
        });
        return option;
    }
}
