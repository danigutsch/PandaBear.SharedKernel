namespace SharedKernel.RepoConfig.Tool;

internal static class Program
{
    public static Task<int> Main(string[] args) =>
        RepoConfigToolApplication.Run(args, Console.Out, Console.Error, Environment.CurrentDirectory, CancellationToken.None);
}
