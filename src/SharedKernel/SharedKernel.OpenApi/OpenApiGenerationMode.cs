using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace PandaBear.OpenApi;

/// <summary>
/// Identifies the ASP.NET Core build-time OpenAPI document generator process.
/// </summary>
public static class OpenApiGenerationMode
{
    private const string DocumentGeneratorAssemblyName = "GetDocument.Insider";

    /// <summary>
    /// The environment name reserved for build-time OpenAPI generation.
    /// </summary>
    public const string HostEnvironmentName = "OpenApiGeneration";

    /// <summary>
    /// Determines whether the current process is the trusted build-time OpenAPI document generator.
    /// </summary>
    /// <param name="environment">The host environment.</param>
    /// <returns><see langword="true" /> only for an explicitly marked document generator process; otherwise, <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="environment"/> is <see langword="null"/>.</exception>
    public static bool IsEnabled(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return IsEnabled(environment, Assembly.GetEntryAssembly()?.GetName().Name);
    }

    /// <summary>Determines generation mode from an explicit entry assembly name, so tests need not depend on the real entry assembly.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="environment"/> is <see langword="null"/>.</exception>
    internal static bool IsEnabled(IHostEnvironment environment, string? entryAssemblyName)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return environment.IsEnvironment(HostEnvironmentName)
               && string.Equals(entryAssemblyName, DocumentGeneratorAssemblyName, StringComparison.Ordinal);
    }
}
