using PandaBear.RepoConfig.Tool;

return await RepoConfigCommandLine.Create(new RepoConfigToolEnvironment(Environment.CurrentDirectory))
    .Parse(args)
    .InvokeAsync().ConfigureAwait(false);
