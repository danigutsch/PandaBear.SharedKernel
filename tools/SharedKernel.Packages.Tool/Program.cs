using PandaBear.Packages.Tool;
using PandaBear.Processes;

return await PackagesCommandLine
    .Create(Environment.CurrentDirectory, PathExecutableResolver.Default, new BoundedProcessRunner())
    .Parse(args)
    .InvokeAsync()
    .ConfigureAwait(false);
