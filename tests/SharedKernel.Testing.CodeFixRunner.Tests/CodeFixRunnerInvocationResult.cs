namespace PandaBear.Testing.CodeFixRunner.Tests;

/// <summary>The exit code and captured output streams of one in-process code fix runner invocation.</summary>
/// <param name="ExitCode">The process exit code returned by the command.</param>
/// <param name="Output">The text written to standard output.</param>
/// <param name="Error">The text written to standard error.</param>
internal sealed record CodeFixRunnerInvocationResult(int ExitCode, string Output, string Error);
