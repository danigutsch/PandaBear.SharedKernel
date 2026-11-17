using PandaBear.Processes;
using PandaBear.Secrets;
using PandaBear.Secrets.Tool;

return await SecretsCommandLine
    .Create(new SecretsEnvironment(
        Environment.CurrentDirectory,
        PathExecutableResolver.Default,
        new SystemToolRunner(),
        Console.OpenStandardInput,
        Console.OpenStandardOutput,
        Environment.GetEnvironmentVariable))
    .Parse(args)
    .InvokeAsync()
    .ConfigureAwait(false);
