namespace PandaBear.Testing.Tests;

/// <summary>Reports whether the host can run POSIX shell scripts, for <c>SkipUnless</c> conditions.</summary>
internal static class PosixHost
{
    /// <summary>Gets a value indicating whether the host is not Windows.</summary>
    public static bool IsPosix => !OperatingSystem.IsWindows();
}
