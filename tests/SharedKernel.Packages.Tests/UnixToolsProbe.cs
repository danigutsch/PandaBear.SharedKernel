namespace PandaBear.Packages.Tests;

/// <summary>Tells tests whether the small Unix tools they run as real processes exist.</summary>
internal static class UnixToolsProbe
{
    /// <summary>The shell.</summary>
    public const string Shell = "/bin/sh";

    /// <summary>A program that writes an endless stream of lines.</summary>
    public const string Yes = "/usr/bin/yes";

    /// <summary>A program that copies the first bytes of its input.</summary>
    public const string Head = "/usr/bin/head";

    /// <summary>Gets a value indicating whether the shell, <c>yes</c>, and <c>head</c> exist.</summary>
    public static bool IsAvailable => File.Exists(Shell) && File.Exists(Yes) && File.Exists(Head);
}
