using PandaBear.Packages.Tool;
using PandaBear.Processes;

return await PackagesCommandLine
    .Create(
        Environment.CurrentDirectory,
        static command => PathExecutableResolver.Default.Find(command),
        new BoundedProcessRunner())
    .Parse(args)
    .InvokeAsync()
    .ConfigureAwait(false);
