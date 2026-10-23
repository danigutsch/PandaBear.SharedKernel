using System.CommandLine;
using PandaBear.VaultManager.Tool.Cli;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool;

/// <summary>Builds and parses the <c>sharedkernel-vault</c> command tree.</summary>
internal static class VaultCommandLine
{
    /// <summary>
    /// Creates the root command with the <c>inspect</c>, <c>scan</c>, and <c>merge</c> commands. Commands write to the
    /// invocation's output and error writers and ask for passwords through <paramref name="environment"/>.
    /// </summary>
    /// <param name="environment">Password prompts, terminal state, and clock of the commands.</param>
    /// <returns>The root command.</returns>
    public static RootCommand Create(VaultCommandEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return new RootCommand("Inspect, scan, and merge password vault exports offline.")
        {
            InspectCommand.Create(environment),
            ScanCommand.Create(environment),
            MergeCommand.Create(environment),
            ReviewCommand.Create(environment),
        };
    }

    /// <summary>
    /// Parses a command line with the parser settings of the tool: System.CommandLine's defaults without response
    /// files, so an argument that starts with <c>@</c> is a file name like any other.
    /// </summary>
    /// <param name="rootCommand">Root command from <see cref="Create"/>.</param>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The parse result, ready to invoke.</returns>
    public static ParseResult Parse(RootCommand rootCommand, IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(rootCommand);
        ArgumentNullException.ThrowIfNull(args);

        var configuration = new ParserConfiguration
        {
            // Response files are off. System.CommandLine would read "@bitwarden.json" as a response file and split the
            // export into arguments, and the errors about those arguments would print vault values. Every argument of
            // this tool is a file name or an option, and passwords are never arguments, so nothing needs one.
            ResponseFileTokenReplacer = null,
        };
        return rootCommand.Parse(args, configuration);
    }
}
