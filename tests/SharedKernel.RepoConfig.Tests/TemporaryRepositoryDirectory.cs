namespace SharedKernel.RepoConfig.Tests;

internal static class TemporaryRepositoryDirectory
{
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
