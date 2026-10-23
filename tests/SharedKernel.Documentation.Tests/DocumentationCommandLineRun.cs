using System.CommandLine;
using System.Globalization;

namespace PandaBear.Documentation.Tests;

internal sealed class DocumentationCommandLineRun
{
    private DocumentationCommandLineRun(int exitCode, string output, string error)
    {
        ExitCode = exitCode;
        Output = output;
        Error = error;
    }

    public int ExitCode { get; }

    public string Output { get; }

    public string Error { get; }

    public static DocumentationCommandLineRun Invoke(string rootPath, IReadOnlyList<string> args) =>
        Invoke(DocumentationCommandLine.Create(rootPath).Parse(args));

    public static DocumentationCommandLineRun Invoke(ParseResult parseResult)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var configuration = new InvocationConfiguration { Output = output, Error = error };

        var exitCode = parseResult.Invoke(configuration);

        return new DocumentationCommandLineRun(exitCode, output.ToString(), error.ToString());
    }
}
