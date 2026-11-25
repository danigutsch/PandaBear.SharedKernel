using System.Collections;
using System.CommandLine;
using SharedKernel.K6.Tool;
using SharedKernel.LoadTesting;
using SharedKernel.LoadTesting.K6;
using SharedKernel.Processes;

var configuration = new InvocationConfiguration
{
    // After Ctrl+C or SIGTERM, stopping k6, removing its container, and stopping the host can take up to 17 seconds.
    // The 2-second default would exit before that cleanup finishes.
    ProcessTerminationTimeout = TimeSpan.FromSeconds(20),
};

// Windows matches variable names without regard to case.
var environment = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
{
    if (variable.Key is string name && variable.Value is string value)
    {
        environment[name] = value;
    }
}

// The health probe reaches only the host: no proxy, and a redirect is not followed.
using var healthCheckHandler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
var runner = new LoadTestRunner(new K6LoadGenerator(new SystemProcessRunner()), PathExecutableResolver.Default, healthCheckHandler);

return await K6CommandLine
    .Create(Environment.CurrentDirectory, environment, runner)
    .Parse(args)
    .InvokeAsync(configuration)
    .ConfigureAwait(false);
