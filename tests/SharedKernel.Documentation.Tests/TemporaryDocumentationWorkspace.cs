using PandaBear.Testing;

namespace PandaBear.Documentation.Tests;

/// <summary>Temporary repository root with a <c>docs/architecture</c> folder, deleted on disposal.</summary>
internal sealed class TemporaryDocumentationWorkspace : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    /// <summary>Creates the temporary root and its <c>docs/architecture</c> directory.</summary>
    public TemporaryDocumentationWorkspace()
    {
        RootPath = _directory.Path;
        Directory.CreateDirectory(Path.Combine(RootPath, "docs", "architecture"));
    }

    /// <summary>Gets the absolute path of the temporary root directory.</summary>
    public string RootPath
    {
        get;
    }

    /// <summary>Writes a file into <c>docs/architecture</c>, replacing any existing content.</summary>
    /// <param name="fileName">The file name inside the architecture folder.</param>
    /// <param name="content">The file content.</param>
    public void WriteArchitectureDoc(string fileName, string content) =>
        File.WriteAllText(Path.Combine(RootPath, "docs", "architecture", fileName), content);

    /// <summary>Writes the generator configuration to <c>docs/architecture/generated-diagrams.json</c>.</summary>
    /// <param name="content">The JSON configuration text.</param>
    public void WriteConfig(string content) =>
        File.WriteAllText(Path.Combine(RootPath, "docs", "architecture", "generated-diagrams.json"), content);

    /// <summary>Writes a file at a path relative to the root, creating missing parent directories first.</summary>
    /// <param name="relativePath">The path below the root.</param>
    /// <param name="content">The file content.</param>
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

    /// <summary>Reads a file from <c>docs/architecture</c>.</summary>
    /// <param name="fileName">The file name inside the architecture folder.</param>
    /// <returns>The file content.</returns>
    public string ReadArchitectureDoc(string fileName) =>
        File.ReadAllText(Path.Combine(RootPath, "docs", "architecture", fileName));

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
