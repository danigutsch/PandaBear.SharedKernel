using Microsoft.CodeAnalysis;

namespace PandaBear.Testing.SourceGenerator;

/// <summary>A diagnostic to report, kept comparable for incremental caching.</summary>
/// <param name="Id">The diagnostic ID.</param>
/// <param name="Location">Where to report it.</param>
/// <param name="Arguments">The message arguments.</param>
internal sealed record DiagnosticInfo(string Id, LocationInfo? Location, EquatableArray<string> Arguments)
{
    /// <summary>Creates the diagnostic.</summary>
    /// <returns>The diagnostic.</returns>
    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(CallProxyDiagnostics.Get(Id), Location?.ToLocation(), [.. Arguments]);
}
