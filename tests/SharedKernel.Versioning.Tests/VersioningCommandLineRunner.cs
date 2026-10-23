using System.CommandLine;
using System.Globalization;
using PandaBear.Versioning.Tool;

namespace PandaBear.Versioning.Tests;

internal static class VersioningCommandLineRunner
{
    public static ParseResult Parse(params string[] args) =>
        VersioningCommandLine.Create(TextReader.Null, TimeProvider.System).Parse(args);

    public static Task<CommandLineInvocation> Invoke(string[] args, CancellationToken ct) =>
        Invoke(args, string.Empty, TimeProvider.System, ct);

    public static Task<CommandLineInvocation> Invoke(string[] args, string input, CancellationToken ct) =>
        Invoke(args, input, TimeProvider.System, ct);

    public static async Task<CommandLineInvocation> Invoke(string[] args, string input, TimeProvider timeProvider, CancellationToken ct)
    {
        using var reader = new StringReader(input);
        return await Invoke(args, reader, timeProvider, ct);
    }

    public static Task<CommandLineInvocation> Invoke(string[] args, TextReader input, CancellationToken ct) =>
        Invoke(args, input, TimeProvider.System, ct);

    public static async Task<CommandLineInvocation> Invoke(string[] args, TextReader input, TimeProvider timeProvider, CancellationToken ct)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var parseResult = VersioningCommandLine.Create(input, timeProvider).Parse(args);
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
