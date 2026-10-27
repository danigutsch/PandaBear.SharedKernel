namespace PandaBear.Repository.Tests;

/// <summary>The outcome of one in-process invocation of the CI tool.</summary>
/// <param name="ExitCode">The exit code that the invocation returned.</param>
/// <param name="Output">The text that the tool wrote to standard output.</param>
/// <param name="Error">The text that the tool wrote to standard error.</param>
internal sealed record RepositoryToolResult(int ExitCode, string Output, string Error);
