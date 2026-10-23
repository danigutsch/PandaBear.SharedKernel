namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>
/// Finds out once per test run which file system features this system offers, so tests that need them are skipped
/// where they are missing instead of failing.
/// </summary>
internal static class FileSystemProbes
{
    private static readonly Lazy<bool> SymbolicLinks = new(ProbeSymbolicLinks);
    private static readonly Lazy<bool> AccessDenial = new(ProbeAccessDenial);

    /// <summary>Whether this process can create a symbolic link to a directory, which Windows allows only to some users.</summary>
    public static bool CanCreateSymbolicLinks => SymbolicLinks.Value;

    /// <summary>
    /// Whether Unix permissions stop this process from reading a file it owns, which is not the case on Windows or for
    /// the root user.
    /// </summary>
    public static bool CanDenyAccess => AccessDenial.Value;

    private static bool ProbeSymbolicLinks()
    {
        string directory = Directory.CreateTempSubdirectory("vault-link-probe-").FullName;
        string target = Directory.CreateDirectory(Path.Combine(directory, "target")).FullName;
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(directory, "link"), target);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static bool ProbeAccessDenial()
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        string directory = Directory.CreateTempSubdirectory("vault-permission-probe-").FullName;
        string file = Path.Combine(directory, "probe.txt");
        try
        {
            File.WriteAllText(file, "probe");
            File.SetUnixFileMode(file, UnixFileMode.None);
            using FileStream stream = File.OpenRead(file);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
