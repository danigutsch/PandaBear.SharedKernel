namespace PandaBear.Packages.Tests;

/// <summary>Tells tests whether the real <c>dpkg-query</c> is available.</summary>
internal static class DpkgProbe
{
    /// <summary>The absolute path of <c>dpkg-query</c>.</summary>
    public const string Path = "/usr/bin/dpkg-query";

    /// <summary>Gets a value indicating whether <c>dpkg-query</c> is installed.</summary>
    public static bool IsInstalled => File.Exists(Path);
}
