namespace PandaBear.Testing;

/// <summary>The outcome of a process run by <see cref="TestProcess" />.</summary>
/// <param name="ExitCode">The exit code of the process.</param>
/// <param name="StandardOutput">The captured standard output.</param>
/// <param name="StandardError">The captured standard error.</param>
public readonly record struct TestProcessResult(int ExitCode, string StandardOutput, string StandardError);
