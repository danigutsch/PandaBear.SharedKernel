using System.CommandLine;
using System.Globalization;

namespace PandaBear.Documentation.Tests;

/// <summary>Captured result of running the documentation command line: exit code, standard output and standard error.</summary>
internal sealed class DocumentationCommandLineRun
{
    private DocumentationCommandLineRun(int exitCode, string output, string error)
    {
        ExitCode = exitCode;
        Output = output;
        Error = error;
    }

    /// <summary>Gets the exit code returned by the command.</summary>
    public int ExitCode
    {
        get;
    }

    /// <summary>Gets the text the command wrote to standard output.</summary>
    public string Output
    {
        get;
    }

    /// <summary>Gets the text the command wrote to standard error.</summary>
    public string Error
    {
        get;
    }

    /// <summary>Parses the arguments against the documentation command tree rooted at the given path and runs the result.</summary>
    /// <param name="rootPath">The repository root the command resolves paths against.</param>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The captured run.</returns>
    public static DocumentationCommandLineRun Invoke(string rootPath, IReadOnlyList<string> args) =>
        Invoke(DocumentationCommandLine.Create(rootPath).Parse(args));

    /// <summary>Runs an already parsed command with its output and error redirected to in-memory writers.</summary>
    /// <param name="parseResult">The parsed command line.</param>
    /// <returns>The captured run.</returns>
    public static DocumentationCommandLineRun Invoke(ParseResult parseResult)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var configuration = new InvocationConfiguration { Output = output, Error = error };

        var exitCode = parseResult.Invoke(configuration);

        return new DocumentationCommandLineRun(exitCode, output.ToString(), error.ToString());
    }
}
