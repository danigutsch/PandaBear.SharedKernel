using System.CommandLine;
using PandaBear.K6.Tool;
using PandaBear.Tooling;

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
        static command => ExecutableResolver.Resolve(command, Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows()))
    .Parse(args)
    .InvokeAsync(configuration)
    .ConfigureAwait(false);
