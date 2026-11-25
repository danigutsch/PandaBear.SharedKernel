namespace SharedKernel.Repositories;

/// <summary>Outcome of a git process run with bounded output capture.</summary>
/// <param name="ExitCode">Process exit code.</param>
/// <param name="StandardOutput">Captured standard output bytes.</param>
/// <param name="StandardError">Captured standard error text.</param>
/// <param name="OutputLimitExceeded">Whether standard output exceeded the capture limit.</param>
/// <param name="ErrorLimitExceeded">Whether standard error exceeded the capture limit.</param>
public sealed record RoadmapGitCommandResult(
    int ExitCode,
    ReadOnlyMemory<byte> StandardOutput,
    string StandardError,
    bool OutputLimitExceeded,
    bool ErrorLimitExceeded);
