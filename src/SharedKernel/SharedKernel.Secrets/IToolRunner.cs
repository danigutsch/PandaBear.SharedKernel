namespace SharedKernel.Secrets;

/// <summary>Runs a child process. Tests replace it with a fake.</summary>
public interface IToolRunner
{
    /// <summary>Runs the request and waits for the process to exit.</summary>
    /// <param name="request">The program, arguments, environment, and standard input.</param>
    /// <param name="cancellationToken">Cancels the wait and ends the process.</param>
    /// <returns>The exit code and output. The caller disposes it.</returns>
    Task<ToolResult> Run(ToolRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Runs the request with the caller's standard input, output, and error, such as a terminal, so a person can work with
    /// the child, and waits for it to exit without a time limit. Nothing the child prints passes through the caller.
    /// </summary>
    /// <param name="request">The program, arguments, and environment. Its standard input must be empty.</param>
    /// <param name="cancellationToken">Cancels the wait and ends the process.</param>
    /// <returns>The exit code.</returns>
    Task<int> RunAttached(ToolRequest request, CancellationToken cancellationToken);
}
