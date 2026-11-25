using SharedKernel.Packages.Tool;
using SharedKernel.Processes;

return await PackagesCommandLine
    .Create(Environment.CurrentDirectory, PathExecutableResolver.Default, new BoundedProcessRunner())
    .Parse(args)
    .InvokeAsync()
    .ConfigureAwait(false);
