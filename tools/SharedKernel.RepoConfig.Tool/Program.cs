namespace PandaBear.RepoConfig.Tool;

internal static class Program
{
    public static Task<int> Main(string[] args) =>
        RepoConfigCommandLine.Create(new RepoConfigToolEnvironment(Environment.CurrentDirectory))
            .Parse(args)
            .InvokeAsync();
}
