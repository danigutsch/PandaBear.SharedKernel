namespace PandaBear.Secrets;

/// <summary>Runs a child process. Tests replace it with a fake.</summary>
public interface IToolRunner
{
    /// <summary>Runs the request and waits for the process to exit.</summary>
    /// <param name="request">The program, arguments, environment, and standard input.</param>
    /// <param name="cancellationToken">Cancels the wait and ends the process.</param>
    /// <returns>The exit code and output. The caller disposes it.</returns>
    Task<ToolResult> Run(ToolRequest request, CancellationToken cancellationToken);
}
