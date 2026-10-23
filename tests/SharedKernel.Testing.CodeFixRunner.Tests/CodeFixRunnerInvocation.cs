using System.CommandLine;
using System.Globalization;

namespace PandaBear.Testing.CodeFixRunner.Tests;

internal static class CodeFixRunnerInvocation
{
    public static async Task<CodeFixRunnerInvocationResult> Invoke(IReadOnlyList<string> args, CancellationToken ct)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var configuration = new InvocationConfiguration
        {
            Output = output,
            Error = error,
            // Signal handlers are process-wide, so test invocations cancel through the token instead.
            ProcessTerminationTimeout = null,
        };

        var exitCode = await CodeFixRunnerCommandLine.Create().Parse(args).InvokeAsync(configuration, ct);

        return new CodeFixRunnerInvocationResult(exitCode, output.ToString(), error.ToString());
    }
}
