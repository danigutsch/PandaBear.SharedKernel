using System.CommandLine;
using SharedKernel.RepoConfig.Tool;

var invocation = RepoConfigCommandLine.Create(new RepoConfigToolEnvironment(Environment.CurrentDirectory)).Parse(args);
if (string.Equals(invocation.CommandResult.Command.Name, RepoConfigCommandLine.IsolatedPodmanCommandName, StringComparison.Ordinal))
{
    // Allow the bounded child shutdown and its two-minute private-engine cleanup after a signal.
    var configuration = new InvocationConfiguration { ProcessTerminationTimeout = TimeSpan.FromMinutes(3) };
    return await invocation.InvokeAsync(configuration).ConfigureAwait(false);
}

return await invocation.InvokeAsync().ConfigureAwait(false);
