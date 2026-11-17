using PandaBear.Results;

namespace PandaBear.Secrets.Sops;

/// <summary>Runs a request and turns a failed run into a result whose message never carries tool output beyond one short line.</summary>
internal static class ProgramInvocation
{
    /// <summary>Runs the request; the caller disposes a successful run.</summary>
    /// <returns>The run on success, otherwise the problem text.</returns>
    internal static async Task<(ToolResult? Run, string? Problem)> Run(IToolRunner runner, string tool, ToolRequest request, CancellationToken cancellationToken)
    {
        ToolResult run;
        try
        {
            run = await runner.Run(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
        {
            return (null, $"{tool} could not run: {exception.Message}");
        }

        if (run.ExitCode == 0)
        {
            return (run, null);
        }

        var problem = run.Describe(tool);
        run.Dispose();
        return (null, problem);
    }

    /// <summary>Checks that <paramref name="path"/> is an absolute file path that is safe to pass as an argument.</summary>
    internal static string? CheckFile(string path) => path switch
    {
        null or "" => "The file path is required.",
        _ when !Path.IsPathFullyQualified(path) => "The file path must be absolute.",
        _ when path.Any(char.IsControl) => "The file path cannot contain control characters.",
        _ => null,
    };

    /// <summary>Creates a failed result with <paramref name="problem"/> as its detail.</summary>
    internal static Result<T> Error<T>(string problem)
        where T : notnull => Result.Error<T>(problem);
}
