namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The tracked hooks directory and its hooks. <c>baseline apply</c> activates it by setting <c>core.hooksPath</c>, the
/// only way the baseline sets that key; it never writes into <c>.git/hooks</c>.
/// </summary>
/// <param name="Hooks">The hook files, written executable on POSIX systems.</param>
/// <param name="Directory">Repository-relative directory that holds the hooks.</param>
/// <param name="OverwriteHooksPath">Allows replacing a different <c>core.hooksPath</c> that the repository already has.</param>
internal sealed record BaselineHooksConfiguration(
    IReadOnlyList<BaselineHookConfiguration> Hooks,
    string Directory = ".githooks",
    bool OverwriteHooksPath = false);
