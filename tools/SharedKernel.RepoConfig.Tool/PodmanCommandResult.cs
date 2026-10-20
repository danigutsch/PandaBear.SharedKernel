namespace SharedKernel.RepoConfig.Tool;

internal sealed record PodmanCommandResult(int ExitCode, string StandardOutput, string StandardError);
