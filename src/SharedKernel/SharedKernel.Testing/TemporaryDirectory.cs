namespace PandaBear.Testing;

/// <summary>Owns a uniquely created temporary directory and deletes it on disposal.</summary>
public sealed class TemporaryDirectory : IDisposable
{
    /// <summary>Creates a directory using the platform temporary directory.</summary>
    public TemporaryDirectory() => Path = Directory.CreateTempSubdirectory("pandabear-").FullName;

    /// <summary>Gets the absolute directory path.</summary>
    public string Path
    {
        get;
    }

    /// <summary>Deletes the directory and nested read-only files without traversing symbolic links.</summary>
    /// <remarks>Cleanup failures propagate to the caller. An already removed directory is accepted.</remarks>
    public void Dispose()
    {
        if (!Directory.Exists(Path))
        {
            return;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false
        };
        foreach (var filePath in Directory.EnumerateFiles(Path, "*", options))
        {
            var attributes = File.GetAttributes(filePath);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
            }
        }

        Directory.Delete(Path, recursive: true);
    }
}
