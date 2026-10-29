namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// A file that <c>baseline apply</c> manages.
/// </summary>
/// <param name="Path">Repository-relative path of the managed file.</param>
/// <param name="Profile">The ID of the embedded profile that supplies the content; exactly one of <paramref name="Profile"/> and <paramref name="Source"/> is set.</param>
/// <param name="Source">Repository-relative path of a template file that supplies the content.</param>
/// <param name="OverwriteExisting">Allows <c>baseline apply</c> to replace an existing file that is unmanaged or was edited by hand.</param>
internal sealed record BaselineFileConfiguration(
    string Path,
    string? Profile = null,
    string? Source = null,
    bool OverwriteExisting = false);
