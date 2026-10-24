namespace PandaBear.Versioning.Tests;

/// <summary>
/// Finds out once per test run whether the file system of the temporary directory tells file names apart by case, so
/// tests that need two such files are skipped where both names would open one file.
/// </summary>
internal static class FileSystemProbes
{
    private static readonly Lazy<bool> CaseSensitivity = new(ProbeCaseSensitivity);

    /// <summary>Whether two file names that differ only in case name two files, which is not the case on Windows.</summary>
    public static bool IsCaseSensitive => CaseSensitivity.Value;

    private static bool ProbeCaseSensitivity()
    {
        var directory = Directory.CreateTempSubdirectory("versioning-case-probe-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "probe"), string.Empty);
            return !File.Exists(Path.Combine(directory, "PROBE"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
