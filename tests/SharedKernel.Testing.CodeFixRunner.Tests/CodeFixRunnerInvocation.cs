using System.CommandLine;
using System.CommandLine.Help;
using System.Globalization;

namespace PandaBear.Testing.CodeFixRunner.Tests;

/// <summary>Parses and invokes the real code-fix runner command tree with captured output.</summary>
internal static class CodeFixRunnerInvocation
{
    /// <summary>Invokes the command line with the real <c>CodeFixRunEngine.Run</c> as the run callback.</summary>
    /// <param name="workingDirectory">The directory against which relative targets are resolved.</param>
    /// <param name="args">The command-line arguments to parse and run.</param>
    /// <param name="ct">The token that cancels the invocation.</param>
    /// <returns>The exit code with the captured standard output and standard error.</returns>
    public static Task<CodeFixRunnerInvocationResult> Invoke(string workingDirectory, IReadOnlyList<string> args, CancellationToken ct)
    {
        return Invoke(workingDirectory, args, CodeFixRunEngine.Run, ct);
    }

    /// <summary>Invokes the command line with unwrapped help text and a caller-supplied run callback, capturing both output streams.</summary>
    /// <param name="workingDirectory">The directory against which relative targets are resolved.</param>
    /// <param name="args">The command-line arguments to parse and run.</param>
    /// <param name="run">The callback that replaces the real code fix run.</param>
    /// <param name="ct">The token that cancels the invocation.</param>
    /// <returns>The exit code with the captured standard output and standard error.</returns>
    public static async Task<CodeFixRunnerInvocationResult> Invoke(
        string workingDirectory,
        IReadOnlyList<string> args,
        Func<CodeFixRunnerOptions, TextWriter, CancellationToken, Task<int?>> run,
        CancellationToken ct)
    {
        var command = CodeFixRunnerCommandLine.Create(workingDirectory, run);

        // Help wraps at the width of the terminal running the tests; unwrapped help keeps assertions independent of it.
        var help = (HelpAction)command.Options.OfType<HelpOption>().First().Action!;
        help.MaxWidth = int.MaxValue;

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var configuration = new InvocationConfiguration
        {
            Output = output,
            Error = error,
            // Signal handlers are process-wide, so test invocations cancel through the token instead.
            ProcessTerminationTimeout = null,
        };

        var exitCode = await command.Parse(args).InvokeAsync(configuration, ct);

        return new CodeFixRunnerInvocationResult(exitCode, output.ToString(), error.ToString());
    }
}
