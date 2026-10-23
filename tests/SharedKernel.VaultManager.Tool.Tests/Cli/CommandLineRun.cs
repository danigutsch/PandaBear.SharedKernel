using System.CommandLine;
using System.CommandLine.Help;
using System.Globalization;
using PandaBear.VaultManager.Tool.Pipeline;

namespace PandaBear.VaultManager.Tool.Tests.Cli;

/// <summary>Exit code and captured output of one command-line invocation.</summary>
/// <param name="ExitCode">Exit code.</param>
/// <param name="Output">Standard output.</param>
/// <param name="Error">Standard error.</param>
internal sealed record CommandLineRun(int ExitCode, string Output, string Error)
{
    /// <summary>Parses and invokes a command line on the real <c>sharedkernel-vault</c> command tree with every output captured.</summary>
    public static Task<CommandLineRun> Invoke(VaultCommandEnvironment environment, IReadOnlyList<string> args, CancellationToken ct) =>
        Invoke(VaultCommandLine.Create(environment), args, ct);

    /// <summary>Parses and invokes a command line on a command tree with every output captured.</summary>
    public static async Task<CommandLineRun> Invoke(RootCommand command, IReadOnlyList<string> args, CancellationToken ct)
    {
        // Help wraps at the width of the terminal running the tests; unwrapped help keeps assertions independent of it.
        var help = (HelpAction)command.Options.OfType<HelpOption>().First().Action!;
        help.MaxWidth = int.MaxValue;

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Signal handlers are process-wide, so test invocations leave Ctrl+C and SIGTERM to the test host.
        var configuration = new InvocationConfiguration { Output = output, Error = error, ProcessTerminationTimeout = null };
        int exitCode = await VaultCommandLine.Parse(command, args).InvokeAsync(configuration, ct);
        return new CommandLineRun(exitCode, output.ToString(), error.ToString());
    }
}
