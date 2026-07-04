using System.Reflection;

namespace SharedKernel.Observability;

internal static class ApplicationVersionProvider
{
    public static string? GetEntryAssemblyInformationalVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        return entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    }
}
