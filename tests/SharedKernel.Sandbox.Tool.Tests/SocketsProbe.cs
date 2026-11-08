namespace PandaBear.Sandbox.Tool.Tests;

/// <summary>Tells tests whether this environment lets a process create, bind, connect, and use a Unix socket.</summary>
internal static class SocketsProbe
{
    /// <summary>Gets a value indicating whether the real round trip works here.</summary>
    public static bool IsAvailable => !OperatingSystem.IsWindows()
        && new SystemSandboxEnvironment().CreateUnixSocket().Outcome == ProbeOutcome.Allowed
        && new SystemSandboxEnvironment().CreateNamedPipe().Outcome == ProbeOutcome.Allowed;
}
