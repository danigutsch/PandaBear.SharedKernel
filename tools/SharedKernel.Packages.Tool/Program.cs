using PandaBear.Packages;
using PandaBear.Packages.Tool;
using PandaBear.Tooling;

return await PackagesCommandLine
    .Create(
        Environment.CurrentDirectory,
        static command => ExecutableResolver.Resolve(command, Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows()),
        new SystemProcessRunner())
    .Parse(args)
    .InvokeAsync()
    .ConfigureAwait(false);
