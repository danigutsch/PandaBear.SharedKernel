using PandaBear.Repository.Tool;

return await RepositoryCommandLine.Create(Environment.CurrentDirectory, ProcessRunner.Run)
    .Parse(args)
    .InvokeAsync()
    .ConfigureAwait(false);
