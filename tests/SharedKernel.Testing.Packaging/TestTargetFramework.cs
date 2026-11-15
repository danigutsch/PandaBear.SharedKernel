using System.Globalization;
using System.Runtime.Versioning;

namespace PandaBear.Testing.Packaging;

/// <summary>The target framework that the running tests were built for, which is the repository's framework.</summary>
public static class TestTargetFramework
{
    /// <summary>Gets the framework moniker of the running tests, such as <c>net11.0</c>.</summary>
    public static string Moniker { get; } = FromName(AppContext.TargetFrameworkName);

    /// <summary>Converts a framework name such as <c>.NETCoreApp,Version=v11.0</c> to its moniker.</summary>
    /// <param name="frameworkName">The framework name.</param>
    /// <returns>The moniker.</returns>
    /// <exception cref="InvalidOperationException">The name is missing or not a .NET framework name.</exception>
    public static string FromName(string? frameworkName)
    {
        if (frameworkName is null)
        {
            throw new InvalidOperationException("The running application has no target framework name.");
        }

        var name = new FrameworkName(frameworkName);
        return name.Identifier == ".NETCoreApp"
            ? string.Create(CultureInfo.InvariantCulture, $"net{name.Version.Major}.{name.Version.Minor}")
            : throw new InvalidOperationException($"'{frameworkName}' is not a .NET framework name.");
    }
}
