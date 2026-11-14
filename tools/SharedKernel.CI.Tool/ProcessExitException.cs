namespace PandaBear.CI.Tool;

/// <summary>A child process started but exited with a non-zero code.</summary>
/// <param name="exitCode">The non-zero exit code of the process.</param>
/// <param name="message">Describes the command and its output.</param>
/// <param name="innerException">The exception that caused this one, if any.</param>
internal sealed class ProcessExitException(int exitCode, string message, Exception? innerException)
    : InvalidOperationException(message, innerException)
{
    private const int UnknownExitCode = -1;

    /// <summary>Creates the exception for a process that exited with <paramref name="exitCode"/>.</summary>
    /// <param name="exitCode">The non-zero exit code of the process.</param>
    /// <param name="message">Describes the command and its output.</param>
    public ProcessExitException(int exitCode, string message)
        : this(exitCode, message, null)
    {
    }

    /// <summary>Creates the exception with no exit code, which reads as -1. Required for exception conventions; prefer the exit-code constructors.</summary>
    public ProcessExitException()
        : this(UnknownExitCode, "A process exited with a non-zero code.")
    {
    }

    /// <summary>Creates the exception with a message and no exit code, which reads as -1.</summary>
    /// <param name="message">Describes the command and its output.</param>
    public ProcessExitException(string message)
        : this(UnknownExitCode, message)
    {
    }

    /// <summary>Creates the exception with a message, a cause, and no exit code, which reads as -1.</summary>
    /// <param name="message">Describes the command and its output.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public ProcessExitException(string message, Exception innerException)
        : this(UnknownExitCode, message, innerException)
    {
    }

    /// <summary>Gets the exit code of the process.</summary>
    public int ExitCode { get; } = exitCode;
}
