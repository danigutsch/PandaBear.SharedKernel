using System.CommandLine;
using PandaBear.VaultManager.Tool.Cli;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Builds root commands whose action runs test work through <see cref="CommandAction"/>.</summary>
internal static class ActionCommands
{
    /// <summary>Creates a root command that runs <paramref name="work"/> with the password prompts of the run.</summary>
    public static RootCommand Running(VaultCommandEnvironment environment, Func<PasswordPrompts, int> work)
    {
        var command = new RootCommand("Runs test work.");
        CommandAction.Set(command, environment, (_, prompts, _) => work(prompts));
        return command;
    }
}
