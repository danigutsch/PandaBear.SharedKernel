namespace SharedKernel.Documentation.Tool;

internal static class Program
{
    public static Task<int> Main(string[] args) =>
        DocumentationToolApplication.Run(args, Console.Out, Console.Error, Environment.CurrentDirectory);
}
