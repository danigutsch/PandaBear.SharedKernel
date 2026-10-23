namespace PandaBear.Repository.Tests;

/// <summary>Builds a platform shell command for child process tests.</summary>
internal static class TestShell
{
    public static (string FileName, string[] Arguments) Command(string bash, string powerShell) =>
        OperatingSystem.IsWindows()
            ? ("pwsh", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", powerShell])
            : ("bash", ["-c", bash]);
}
