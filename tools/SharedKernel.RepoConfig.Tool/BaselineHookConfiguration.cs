namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// A Git hook file in the tracked hooks directory.
/// </summary>
/// <param name="Name">The hook name, such as <c>pre-commit</c>, from the list of Git hook names.</param>
/// <param name="Profile">The ID of the embedded profile that supplies the content; exactly one of <paramref name="Profile"/> and <paramref name="Source"/> is set.</param>
/// <param name="Source">Repository-relative path of a template file that supplies the content.</param>
/// <param name="OverwriteExisting">Allows replacing an existing hook file that is unmanaged or was edited by hand.</param>
internal sealed record BaselineHookConfiguration(
    string Name,
    string? Profile = null,
    string? Source = null,
    bool OverwriteExisting = false);
