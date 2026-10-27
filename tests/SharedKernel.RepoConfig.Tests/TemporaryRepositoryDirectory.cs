namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// Cleanup helpers that delete temporary repository files and directories even when Git marked them read-only.
/// </summary>
internal static class TemporaryRepositoryDirectory
{
    /// <summary>
    /// Deletes the directory recursively after clearing read-only attributes on its files, and does nothing when it does not exist.
    /// </summary>
    public static void Delete(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false
        };
        foreach (var filePath in Directory.EnumerateFiles(path, "*", options))
        {
            MakeWritable(filePath);
        }

        Directory.Delete(path, recursive: true);
    }

    /// <summary>
    /// Deletes the file after clearing its read-only attribute when it exists.
    /// </summary>
    public static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            MakeWritable(path);
        }

        File.Delete(path);
    }

    private static void MakeWritable(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }
}
