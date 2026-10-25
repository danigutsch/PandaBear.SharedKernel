namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Options of <c>verify-devcontainer</c>.
/// </summary>
/// <param name="Root">The absolute repository root, which is the dev container workspace folder.</param>
/// <param name="Commands">The commands to run with <c>bash -lc</c> inside the container, in order.</param>
/// <param name="LogDirectory">The absolute directory that receives the logs and <c>metadata.txt</c>.</param>
/// <param name="KeepContainer">Whether to keep the container instead of removing it.</param>
/// <param name="DevcontainerCli">The absolute Dev Container CLI path, or <see langword="null"/> to bootstrap the pinned CLI.</param>
internal sealed record VerifyDevcontainerOptions(
    string Root,
    IReadOnlyList<string> Commands,
    string LogDirectory,
    bool KeepContainer,
    string? DevcontainerCli);
