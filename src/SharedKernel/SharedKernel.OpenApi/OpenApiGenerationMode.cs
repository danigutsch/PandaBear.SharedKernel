using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace SharedKernel.OpenApi;

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
    public static bool IsEnabled(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return IsEnabled(environment, Assembly.GetEntryAssembly()?.GetName().Name);
    }

    internal static bool IsEnabled(IHostEnvironment environment, string? entryAssemblyName)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return environment.IsEnvironment(HostEnvironmentName)
               && string.Equals(entryAssemblyName, DocumentGeneratorAssemblyName, StringComparison.Ordinal);
    }
}
