namespace PandaBear.Documentation.Tool;

internal static class Program
{
    public static int Main(string[] args) =>
        DocumentationCommandLine.Create(Environment.CurrentDirectory).Parse(args).Invoke();
}
