using System.CommandLine;
using PandaBear.VaultManager.Tool.Cli;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool;

/// <summary>Builds the <c>sharedkernel-vault</c> command tree.</summary>
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
        };
    }
}
