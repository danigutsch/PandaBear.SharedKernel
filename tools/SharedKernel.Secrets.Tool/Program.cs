using SharedKernel.Processes;
using SharedKernel.Secrets;
using SharedKernel.Secrets.Tool;

return await SecretsCommandLine
    .Create(new SecretsEnvironment(
        Environment.CurrentDirectory,
        PathExecutableResolver.Default,
        new SystemToolRunner(),
        Console.OpenStandardInput,
        Console.OpenStandardOutput,
        Environment.GetEnvironmentVariable,
        static () => !Console.IsInputRedirected && !Console.IsOutputRedirected))
    .Parse(args)
    .InvokeAsync()
    .ConfigureAwait(false);
