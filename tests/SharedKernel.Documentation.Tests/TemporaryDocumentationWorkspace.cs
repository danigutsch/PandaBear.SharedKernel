using PandaBear.Testing;

namespace PandaBear.Documentation.Tests;

internal sealed class TemporaryDocumentationWorkspace : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    public TemporaryDocumentationWorkspace()
    {
        RootPath = _directory.Path;
        Directory.CreateDirectory(Path.Combine(RootPath, "docs", "architecture"));
    }

    public string RootPath { get; }

    public void WriteArchitectureDoc(string fileName, string content) =>
        File.WriteAllText(Path.Combine(RootPath, "docs", "architecture", fileName), content);

    public void WriteConfig(string content) =>
        File.WriteAllText(Path.Combine(RootPath, "docs", "architecture", "generated-diagrams.json"), content);

    public void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(RootPath, relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
    }

    public string ReadArchitectureDoc(string fileName) =>
        File.ReadAllText(Path.Combine(RootPath, "docs", "architecture", fileName));

    public void Dispose() => _directory.Dispose();
}
