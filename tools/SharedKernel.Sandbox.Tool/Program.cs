using PandaBear.Sandbox.Tool;

return await SandboxCommandLine
    .Create(Environment.CurrentDirectory, new SystemSandboxEnvironment())
    .Parse(args)
    .InvokeAsync()
    .ConfigureAwait(false);
