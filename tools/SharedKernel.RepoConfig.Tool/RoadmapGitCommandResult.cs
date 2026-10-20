namespace SharedKernel.RepoConfig.Tool;

internal sealed record RoadmapGitCommandResult(
    int ExitCode,
    byte[] StandardOutput,
    string StandardError,
    bool OutputLimitExceeded,
    bool ErrorLimitExceeded);
