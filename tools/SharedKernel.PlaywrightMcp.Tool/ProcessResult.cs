namespace PandaBear.PlaywrightMcp.Tool;

/// <summary>Outcome of a finished process.</summary>
/// <param name="ExitCode">Process exit code.</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error.</param>
internal readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);
