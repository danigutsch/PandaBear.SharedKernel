using PandaBear.Secrets;
using PandaBear.Secrets.Tool;
using PandaBear.Tooling;

return await SecretsCommandLine
    .Create(new SecretsEnvironment(
        Environment.CurrentDirectory,
        static command => ExecutableResolver.Resolve(command, Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows()),
        new SystemToolRunner(),
        Console.OpenStandardInput,
        Console.OpenStandardOutput,
        Environment.GetEnvironmentVariable))
    .Parse(args)
    .InvokeAsync()
    .ConfigureAwait(false);
