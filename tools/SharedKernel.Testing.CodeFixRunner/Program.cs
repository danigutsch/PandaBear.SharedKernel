using PandaBear.Testing.CodeFixRunner;

return await CodeFixRunnerCommandLine.Create(CodeFixRunEngine.Run).Parse(args).InvokeAsync().ConfigureAwait(false);
