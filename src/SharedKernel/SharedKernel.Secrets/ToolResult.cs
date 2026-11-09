namespace PandaBear.Secrets;

/// <summary>The result of a finished child process. Dispose it to overwrite the standard output bytes.</summary>
public sealed class ToolResult : IDisposable
{
    /// <summary>Creates a result.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <param name="standardOutput">The standard output bytes; the result owns and clears them.</param>
    /// <param name="standardError">The first part of the standard error text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="standardOutput"/> or <paramref name="standardError"/> is <see langword="null"/>.</exception>
    public ToolResult(int exitCode, SecretBytes standardOutput, string standardError = "")
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    /// <summary>Gets the exit code.</summary>
    public int ExitCode
    {
        get;
    }

    /// <summary>Gets the standard output bytes, which can be plaintext.</summary>
    public SecretBytes StandardOutput
    {
        get;
    }

    /// <summary>Gets the first part of the standard error text. A tool does not print plaintext there, but treat it as unfiltered tool output.</summary>
    public string StandardError
    {
        get;
    }

    /// <inheritdoc />
    public void Dispose() => StandardOutput.Dispose();
}
