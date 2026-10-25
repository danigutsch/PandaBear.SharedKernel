using System.CommandLine;
using PandaBear.CI.Tool;

// A cancelled run stops its services before it exits, and the real one needs about ten seconds to remove its container, but
// System.CommandLine ends the process two seconds after a signal unless it is told otherwise. Ninety seconds cover the default
// stop timeout of a service (60), the kill of the running test process, the draining of the logs, and the evidence.
// ponytail: one fixed budget for every command, and System.CommandLine swallows a second signal, so a stuck cleanup holds the
// terminal for that long. A service that needs longer is left to finish alone, as it was before, and its closed input still
// tells it to stop. Derive the budget from the services' stopTimeoutSeconds if that ever matters.
var configuration = new InvocationConfiguration { ProcessTerminationTimeout = TimeSpan.FromSeconds(90) };
return await RepositoryCommandLine.Create(Environment.CurrentDirectory, ProcessRunner.Run)
    .Parse(args)
    .InvokeAsync(configuration)
    .ConfigureAwait(false);
