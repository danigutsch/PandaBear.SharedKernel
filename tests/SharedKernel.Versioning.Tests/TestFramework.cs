using System.Runtime.Versioning;

namespace PandaBear.Versioning.Tests;

/// <summary>The target framework moniker of the running test, which the SDK of every fixture can build.</summary>
internal static class TestFramework
{
    /// <summary>Gets the running test's framework as <c>net{Major}.{Minor}</c>.</summary>
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var name = new FrameworkName(AppContext.TargetFrameworkName!);
        return "net" + name.Version.Major + "." + name.Version.Minor;
    }
}
