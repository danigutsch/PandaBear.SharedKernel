using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Defines the explicitly opt-in configuration used while ASP.NET Core generates OpenAPI documents at build time.
/// </summary>
public static class OpenApiBuildGeneration
{
    private const string DocumentGeneratorAssemblyName = "GetDocument.Insider";

    /// <summary>
    /// The configuration key that enables build-time OpenAPI generation behavior.
    /// </summary>
    public const string ConfigurationKey = "OpenApi:BuildGeneration";

    /// <summary>
    /// The environment-variable form of <see cref="ConfigurationKey" />.
    /// </summary>
    public const string EnvironmentVariableName = "OpenApi__BuildGeneration";

    /// <summary>
    /// The deterministic authority used only while generating OpenAPI documents.
    /// </summary>
    public const string PlaceholderAuthority = "https://openapi.invalid";

    /// <summary>
    /// The deterministic issuer used only while generating OpenAPI documents.
    /// </summary>
    public const string PlaceholderIssuer = "https://openapi.invalid";

    /// <summary>
    /// Determines whether explicitly configured build-time OpenAPI generation behavior is enabled.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns><see langword="true" /> when build-time OpenAPI generation is enabled; otherwise, <see langword="false" />.</returns>
    public static bool IsEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return IsEnabled(configuration, Assembly.GetEntryAssembly()?.GetName().Name);
    }

    internal static bool IsEnabled(IConfiguration configuration, string? entryAssemblyName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return bool.TryParse(configuration[ConfigurationKey], out var enabled)
               && enabled
               && string.Equals(entryAssemblyName, DocumentGeneratorAssemblyName, StringComparison.Ordinal);
    }
}
