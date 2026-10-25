namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Locates a Dev Container CLI to start.
/// </summary>
/// <param name="Executable">The absolute path of the executable to start.</param>
/// <param name="Script">The CLI entry point that <paramref name="Executable"/> runs as its first argument, or <see langword="null"/> when the executable is the CLI.</param>
internal sealed record DevcontainerCli(string Executable, string? Script);
