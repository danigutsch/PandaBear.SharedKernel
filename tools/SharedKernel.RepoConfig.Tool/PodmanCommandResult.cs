namespace PandaBear.RepoConfig.Tool;

/// <summary>Outcome of a finished Podman command.</summary>
/// <param name="ExitCode">Process exit code.</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error.</param>
internal sealed record PodmanCommandResult(int ExitCode, string StandardOutput, string StandardError);
