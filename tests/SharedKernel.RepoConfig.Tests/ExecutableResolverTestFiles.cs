namespace PandaBear.RepoConfig.Tests;

/// <summary>Creates executable files for the executable resolver tests.</summary>
internal static class ExecutableResolverTestFiles
{
    /// <summary>Writes an empty file that Unix marks executable for its owner, and returns its path.</summary>
    public static string WriteExecutable(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, string.Empty);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }
}
