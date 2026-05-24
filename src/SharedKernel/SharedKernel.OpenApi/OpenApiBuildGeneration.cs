using System.Reflection;

namespace SharedKernel.OpenApi;

/// <summary>
/// Detects whether the current process is the ASP.NET Core build-time OpenAPI generator.
/// </summary>
public static class OpenApiBuildGeneration
{
    /// <summary>
    /// Returns a value indicating whether the current entry assembly is the build-time OpenAPI generator host.
    /// </summary>
    /// <returns><see langword="true"/> when running under build-time OpenAPI generation; otherwise <see langword="false"/>.</returns>
    public static bool IsRunningBuildTimeDocumentGeneration()
    {
        return string.Equals(
            Assembly.GetEntryAssembly()?.GetName().Name,
            "GetDocument.Insider",
            StringComparison.Ordinal);
    }
}
