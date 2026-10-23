using PandaBear.Testing.CodeFixRunner;

return await CodeFixRunnerCommandLine.Create(Environment.CurrentDirectory, CodeFixRunEngine.Run)
    .Parse(args)
    .InvokeAsync()
    .ConfigureAwait(false);
