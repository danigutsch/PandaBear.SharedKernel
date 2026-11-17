using PandaBear.Secrets;
using PandaBear.Secrets.Tool;
using PandaBear.Processes;

return await SecretsCommandLine
    .Create(new SecretsEnvironment(
        Environment.CurrentDirectory,
        PathExecutableResolver.Default.Find,
        new SystemToolRunner(),
        Console.OpenStandardInput,
        Console.OpenStandardOutput,
        Environment.GetEnvironmentVariable))
    .Parse(args)
    .InvokeAsync()
    .ConfigureAwait(false);
