namespace PandaBear.Versioning.Tests;

/// <summary>
/// Result of running the versioning command line in process.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="Output">The text written to standard output.</param>
/// <param name="Error">The text written to standard error.</param>
internal sealed record CommandLineInvocation(int ExitCode, string Output, string Error);
