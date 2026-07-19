namespace SharedKernel.PlaywrightMcp.Tool;

internal readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);
