namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// Disposable temporary directory with file read, write, and delete helpers for RepoConfig tests.
/// </summary>
internal sealed class TemporaryRepoConfigWorkspace : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    /// <summary>
    /// Creates a unique temporary workspace directory.
    /// </summary>
    public TemporaryRepoConfigWorkspace()
    {
        RootPath = _directory.Path;
    }

    /// <summary>
    /// Gets the absolute path of the workspace directory.
    /// </summary>
    public string RootPath
    {
        get;
    }

    /// <summary>
    /// Returns whether a file exists at the workspace-relative path.
    /// </summary>
    public bool FileExists(string relativePath) =>
        File.Exists(Path.Combine(RootPath, relativePath));

    /// <summary>
    /// Reads the text of a workspace-relative file.
    /// </summary>
    public string ReadFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RootPath, relativePath));

    /// <summary>
    /// Writes text to a workspace-relative file, creating missing directories.
    /// </summary>
    public void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? RootPath);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Deletes a workspace-relative file.
    /// </summary>
    public void DeleteFile(string relativePath) =>
        File.Delete(Path.Combine(RootPath, relativePath));

    /// <summary>
    /// Deletes the workspace directory when it exists.
    /// </summary>
    public void Dispose() => _directory.Dispose();
}
