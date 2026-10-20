using System.Reflection;

namespace PandaBear.Observability;

internal static class ApplicationVersionProvider
{
    public static string? GetEntryAssemblyInformationalVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        return entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    }
}
