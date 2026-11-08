namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>Tells tests whether file permissions can be used to force a refusal.</summary>
internal static class PermissionsProbe
{
    /// <summary>Gets a value indicating whether permissions are enforced: a Unix system and a process without extra privilege.</summary>
    public static bool CanRestrict => !OperatingSystem.IsWindows() && !Environment.IsPrivilegedProcess;
}
