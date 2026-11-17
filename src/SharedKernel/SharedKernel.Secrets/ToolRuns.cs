using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>
/// What every provider does around an <see cref="IToolRunner"/>: run a program and turn any failure into a result whose
/// message is one short line that never carries the program's output, and check a file path before it becomes an
/// argument.
/// </summary>
public static class ToolRuns
{
    /// <summary>
    /// Runs <paramref name="request"/> and returns the finished run when the program exits with code 0. A program that
    /// cannot start, times out, writes too much, or exits with another code is an error described by
    /// <see cref="ToolResult.Describe"/> or by the runner's message.
    /// </summary>
    /// <param name="runner">The runner.</param>
    /// <param name="tool">The program name for messages, such as <c>sops</c>.</param>
    /// <param name="request">The run.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The successful run, which the caller disposes, or the error.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tool"/> is empty or white space.</exception>
    public static async Task<Result<ToolResult>> RunToSuccess(this IToolRunner runner, string tool, ToolRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        ArgumentNullException.ThrowIfNull(request);
        ToolResult run;
        try
        {
            run = await runner.Run(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
        {
            return Result.Error<ToolResult>($"{tool} could not run: {exception.Message}");
        }

        if (run.ExitCode == 0)
        {
            return Result.Ok(run);
        }

        var problem = run.Describe(tool);
        run.Dispose();
        return Result.Error<ToolResult>(problem);
    }

    /// <summary>Returns why <paramref name="path"/> cannot be passed as a file argument, or <see langword="null"/> when it can.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The reason, or <see langword="null"/> for an absolute path without control characters.</returns>
    public static string? CheckFileArgument(string? path) => path switch
    {
        null or "" => "The file path is required.",
        _ when !Path.IsPathFullyQualified(path) => "The file path must be absolute.",
        _ when path.Any(char.IsControl) => "The file path cannot contain control characters.",
        _ => null,
    };
}
