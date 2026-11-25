using System.CommandLine;
using SharedKernel.PlaywrightMcp;
using SharedKernel.PlaywrightMcp.Tool;
using SharedKernel.Processes;

var configuration = new InvocationConfiguration
{
    // After Ctrl+C or SIGTERM, stopping the runtime and force-removing its container can take up to
    // 16 seconds. The 2-second default would exit before container removal finishes or reports failure.
    ProcessTerminationTimeout = TimeSpan.FromSeconds(20)
};

var server = new PlaywrightMcpServer(new SystemProcessRunner(), PathExecutableResolver.Default);
return await PlaywrightMcpCommandLine
    .Create(server, Environment.GetEnvironmentVariable)
    .Parse(args)
    .InvokeAsync(configuration)
    .ConfigureAwait(false);
