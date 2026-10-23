using PandaBear.Testing.CodeFixRunner;

return await CodeFixRunnerCommandLine.Create().Parse(args).InvokeAsync().ConfigureAwait(false);
