namespace SharedKernel.Processes;

/// <summary>The exit code, the exact standard output bytes, and the standard error text of a finished child process.</summary>
public sealed class CapturedProcessOutput
{
    /// <summary>Creates a result.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <param name="standardOutput">The bytes the process wrote to standard output, unchanged.</param>
    /// <param name="standardError">What the process wrote to standard error, decoded as UTF-8.</param>
    /// <exception cref="ArgumentNullException"><paramref name="standardError"/> is <see langword="null"/>.</exception>
    public CapturedProcessOutput(int exitCode, ReadOnlyMemory<byte> standardOutput, string standardError)
    {
        ArgumentNullException.ThrowIfNull(standardError);
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    /// <summary>Gets the exit code.</summary>
    public int ExitCode { get; }

    /// <summary>Gets the bytes the process wrote to standard output, unchanged, so NUL separators and invalid UTF-8 survive.</summary>
    public ReadOnlyMemory<byte> StandardOutput { get; }

    /// <summary>Gets what the process wrote to standard error, decoded as UTF-8 with invalid bytes replaced.</summary>
    public string StandardError { get; }
}
