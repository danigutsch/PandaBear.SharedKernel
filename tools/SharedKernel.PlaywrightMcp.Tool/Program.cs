using System.CommandLine;
using PandaBear.PlaywrightMcp.Tool;

var configuration = new InvocationConfiguration
{
    // After Ctrl+C or SIGTERM, stopping the runtime and force-removing its container can take up to
    // 16 seconds. The 2-second default would exit before container removal finishes or reports failure.
    ProcessTerminationTimeout = TimeSpan.FromSeconds(20)
};

return await PlaywrightMcpCommandLine
    .Create(Environment.GetEnvironmentVariable, PlaywrightMcpApplication.ResolveExecutable, PlaywrightMcpApplication.RunProcess)
    .Parse(args)
    .InvokeAsync(configuration)
    .ConfigureAwait(false);
