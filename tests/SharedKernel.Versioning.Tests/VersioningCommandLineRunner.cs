using System.CommandLine;
using System.CommandLine.Help;
using System.Globalization;
using PandaBear.Versioning.Tool;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// Builds and invokes the versioning command line in process for tests.
/// </summary>
internal static class VersioningCommandLineRunner
{
    /// <summary>
    /// Creates the versioning root command with empty input and the system clock.
    /// </summary>
    public static RootCommand Create() => VersioningCommandLine.Create(TextReader.Null, TimeProvider.System);

    /// <summary>
    /// Parses the arguments with a new root command without invoking it.
    /// </summary>
    public static ParseResult Parse(params string[] args) => Create().Parse(args);

    /// <summary>
    /// Invokes the command line with empty input and the system clock.
    /// </summary>
    public static Task<CommandLineInvocation> Invoke(string[] args, CancellationToken ct) =>
        Invoke(args, string.Empty, TimeProvider.System, ct);

    /// <summary>
    /// Invokes the command line with the given standard input text and the system clock.
    /// </summary>
    public static Task<CommandLineInvocation> Invoke(string[] args, string input, CancellationToken ct) =>
        Invoke(args, input, TimeProvider.System, ct);

    /// <summary>
    /// Invokes the command line with the given standard input text and time provider.
    /// </summary>
    public static async Task<CommandLineInvocation> Invoke(string[] args, string input, TimeProvider timeProvider, CancellationToken ct)
    {
        using var reader = new StringReader(input);
        return await Invoke(args, reader, timeProvider, ct);
    }

    /// <summary>
    /// Invokes the command line with the given input reader and the system clock.
    /// </summary>
    public static Task<CommandLineInvocation> Invoke(string[] args, TextReader input, CancellationToken ct) =>
        Invoke(args, input, TimeProvider.System, ct);

    /// <summary>
    /// Invokes the command line in process with unwrapped help, capturing exit code, output, and error.
    /// </summary>
    public static async Task<CommandLineInvocation> Invoke(string[] args, TextReader input, TimeProvider timeProvider, CancellationToken ct)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var command = VersioningCommandLine.Create(input, timeProvider);

        // Help wraps at the width of the terminal running the tests; unwrapped help keeps assertions independent of it.
        var help = (HelpAction)command.Options.OfType<HelpOption>().First().Action!;
        help.MaxWidth = int.MaxValue;
        var parseResult = command.Parse(args);
        var configuration = new InvocationConfiguration
        {
            Output = output,
            Error = error,
            // Signal handlers are process-wide, so test invocations do not register them.
            ProcessTerminationTimeout = null,
        };
        var exitCode = await parseResult.InvokeAsync(configuration, ct);
        return new CommandLineInvocation(exitCode, output.ToString(), error.ToString());
    }
}
