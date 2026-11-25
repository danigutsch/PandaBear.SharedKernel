namespace SharedKernel.Secrets.Sops.Tests;

/// <summary>Tells tests whether files have Unix modes.</summary>
internal static class UnixModes
{
    /// <summary>Gets a value indicating whether the platform has Unix file modes.</summary>
    public static bool Available => !OperatingSystem.IsWindows();
}
