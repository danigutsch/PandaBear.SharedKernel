using System.CommandLine;
using System.CommandLine.Help;
using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Parses and invokes the real CI tool command tree with captured output.</summary>
internal static class RepositoryToolInvocation
{
    public static async Task<RepositoryToolResult> Run(
        string workingDirectory,
        IReadOnlyList<string> args,
        Func<string, string[], string, CancellationToken, Task<string>> run,
        CancellationToken ct)
    {
        var command = RepositoryCommandLine.Create(workingDirectory, run);

        // Help wraps at the width of the terminal running the tests; unwrapped help keeps assertions independent of it.
        var help = (HelpAction)command.Options.OfType<HelpOption>().First().Action!;
        help.MaxWidth = int.MaxValue;

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Signal handlers are process-wide, so test invocations leave Ctrl+C and SIGTERM to the test host.
        var configuration = new InvocationConfiguration { Output = output, Error = error, ProcessTerminationTimeout = null };
        var exitCode = await command.Parse(args).InvokeAsync(configuration, ct);
        return new RepositoryToolResult(exitCode, output.ToString(), error.ToString());
    }
}
