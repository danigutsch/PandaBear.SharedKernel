namespace PandaBear.Secrets.Tests;

/// <summary>Tells tests whether the small Unix tools they run as real processes exist.</summary>
internal static class UnixToolsProbe
{
    /// <summary>The shell.</summary>
    public const string Shell = "/bin/sh";

    /// <summary>A program that copies its input to its output.</summary>
    public const string Cat = "/bin/cat";

    /// <summary>A program that prints its environment.</summary>
    public const string Env = "/usr/bin/env";

    /// <summary>A program that writes an endless stream of lines.</summary>
    public const string Yes = "/usr/bin/yes";

    /// <summary>Gets a value indicating whether the shell, <c>cat</c>, <c>env</c>, and <c>yes</c> exist.</summary>
    public static bool IsAvailable => File.Exists(Shell) && File.Exists(Cat) && File.Exists(Env) && File.Exists(Yes);
}
