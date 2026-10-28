namespace PandaBear.Testing;

/// <summary>Detects file-system capabilities once per process so tests can skip when the host lacks them. A probe that cannot run reports the capability as missing.</summary>
public static class FileSystemProbes
{
    private static readonly Lazy<bool> SymbolicLinks = new(ProbeSymbolicLinks);
    private static readonly Lazy<bool> AccessDenial = new(ProbeAccessDenial);
    private static readonly Lazy<bool> CaseSensitivity = new(ProbeCaseSensitivity);

    /// <summary>Gets a value indicating whether the process can create symbolic links.</summary>
    public static bool CanCreateSymbolicLinks => SymbolicLinks.Value;

    /// <summary>Gets a value indicating whether removing all permissions from a file makes it unreadable to the process.</summary>
    public static bool CanDenyAccess => AccessDenial.Value;

    /// <summary>Gets a value indicating whether the temporary-file volume distinguishes file names by case.</summary>
    public static bool IsCaseSensitive => CaseSensitivity.Value;

    private static bool ProbeSymbolicLinks()
    {
        try
        {
            using var directory = new TemporaryDirectory();
            var target = Directory.CreateDirectory(Path.Combine(directory.Path, "target")).FullName;
            Directory.CreateSymbolicLink(Path.Combine(directory.Path, "link"), target);
            return true;
        }
        catch (Exception exception) when (IsProbeFailure(exception))
        {
            return false;
        }
    }

    private static bool ProbeAccessDenial()
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var directory = new TemporaryDirectory();
            var file = Path.Combine(directory.Path, "probe.txt");
            File.WriteAllText(file, "probe");
            File.SetUnixFileMode(file, UnixFileMode.None);
            using var stream = File.OpenRead(file);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (Exception exception) when (IsProbeFailure(exception))
        {
            return false;
        }
    }

    private static bool ProbeCaseSensitivity()
    {
        try
        {
            using var directory = new TemporaryDirectory();
            File.WriteAllText(Path.Combine(directory.Path, "probe"), string.Empty);
            return !File.Exists(Path.Combine(directory.Path, "PROBE"));
        }
        catch (Exception exception) when (IsProbeFailure(exception))
        {
            return false;
        }
    }

    private static bool IsProbeFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException;
}
