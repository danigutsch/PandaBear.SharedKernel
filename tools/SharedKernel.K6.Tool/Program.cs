using System.CommandLine;
using PandaBear.K6.Tool;
using PandaBear.Processes;

var configuration = new InvocationConfiguration
{
    // After Ctrl+C or SIGTERM, stopping k6, removing its container, and stopping the host can take up to 17 seconds.
    // The 2-second default would exit before that cleanup finishes.
    ProcessTerminationTimeout = TimeSpan.FromSeconds(20),
};

return await K6CommandLine
    .Create(
        Environment.CurrentDirectory,
        Environment.GetEnvironmentVariable,
        static command => PathExecutableResolver.Default.Find(command))
    .Parse(args)
    .InvokeAsync(configuration)
    .ConfigureAwait(false);
