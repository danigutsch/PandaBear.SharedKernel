namespace PandaBear.RepoConfig.Tests;

/// <summary>Skip conditions for tests that need POSIX file permissions that bind the current user.</summary>
internal static class PosixTestSupport
{
    /// <summary>Gets whether file modes exist and the current user cannot bypass them.</summary>
    public static bool CanRestrictPermissions => !OperatingSystem.IsWindows() && !string.Equals(Environment.UserName, "root", StringComparison.Ordinal);
}
