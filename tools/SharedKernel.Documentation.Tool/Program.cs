namespace PandaBear.Documentation.Tool;

/// <summary>Entry point of the documentation tool.</summary>
internal static class Program
{
    /// <summary>Parses the command line and runs the documentation command, returning its exit code.</summary>
    /// <param name="args">Command-line arguments.</param>
    public static int Main(string[] args) =>
        DocumentationCommandLine.Create(Environment.CurrentDirectory).Parse(args).Invoke();
}
