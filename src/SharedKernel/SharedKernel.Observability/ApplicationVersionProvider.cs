using System.Reflection;

namespace PandaBear.Observability;

/// <summary>Reads version information of the running application.</summary>
internal static class ApplicationVersionProvider
{
    /// <summary>Returns the informational version of the entry assembly, or null when it has none.</summary>
    public static string? GetEntryAssemblyInformationalVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        return entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    }
}
