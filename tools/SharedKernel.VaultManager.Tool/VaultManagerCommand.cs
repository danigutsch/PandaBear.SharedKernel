using System.CommandLine;

namespace PandaBear.VaultManager.Tool;

/// <summary>Builds the <c>sharedkernel-vault</c> command tree.</summary>
internal static class VaultManagerCommand
{
    /// <summary>Creates the root command with every subcommand.</summary>
    public static RootCommand Create() => new("Inspect, review, deduplicate, and merge password vault exports offline.");
}
