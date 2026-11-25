namespace SharedKernel.Processes;

/// <summary>The exit code and collected output of a finished child process.</summary>
public sealed class ProcessOutput
{
    /// <summary>Creates a result.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <param name="standardOutput">What the process wrote to standard output, or an empty string when the output was forwarded or not redirected.</param>
    /// <param name="standardError">What the process wrote to standard error, or an empty string when the output was forwarded or not redirected.</param>
    /// <exception cref="ArgumentNullException"><paramref name="standardOutput"/> or <paramref name="standardError"/> is <see langword="null"/>.</exception>
    public ProcessOutput(int exitCode, string standardOutput, string standardError = "")
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    /// <summary>Gets the exit code.</summary>
    public int ExitCode { get; }

    /// <summary>Gets everything the process wrote to standard output.</summary>
    public string StandardOutput { get; }

    /// <summary>Gets everything the process wrote to standard error.</summary>
    public string StandardError { get; }
}
