namespace PandaBear.Repository.Tests;

/// <summary>Builds a platform shell command for child process tests.</summary>
internal static class TestShell
{
    /// <summary>Returns <c>pwsh -Command</c> with <paramref name="powerShell"/> on Windows and <c>bash -c</c> with <paramref name="bash"/> elsewhere.</summary>
    public static (string FileName, string[] Arguments) Command(string bash, string powerShell) =>
        OperatingSystem.IsWindows()
            ? ("pwsh", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", powerShell])
            : ("bash", ["-c", bash]);
}
