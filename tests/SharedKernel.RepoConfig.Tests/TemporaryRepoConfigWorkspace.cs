namespace SharedKernel.RepoConfig.Tests;

internal sealed class TemporaryRepoConfigWorkspace : IDisposable
{
    public TemporaryRepoConfigWorkspace()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "sharedkernel-repo-config-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public bool FileExists(string relativePath) =>
        File.Exists(Path.Combine(RootPath, relativePath));

    public string ReadFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RootPath, relativePath));

    public void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? RootPath);
        File.WriteAllText(path, content);
    }

    public void DeleteFile(string relativePath) =>
        File.Delete(Path.Combine(RootPath, relativePath));

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
