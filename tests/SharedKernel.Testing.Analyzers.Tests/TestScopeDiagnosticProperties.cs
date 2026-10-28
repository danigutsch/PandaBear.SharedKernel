using System.Collections.Immutable;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Builds the diagnostic properties SKTEST018 attaches for its code fix.</summary>
internal static class TestScopeDiagnosticProperties
{
    /// <summary>Creates the properties for a diagnostic with the given reason and allowed scopes.</summary>
    /// <param name="reason">One of missing, unknown or conflicting.</param>
    /// <param name="allowedScopes">Semicolon-separated allowed scope values.</param>
    /// <param name="typed">True when the typed attribute is referenced.</param>
    public static ImmutableDictionary<string, string?> Create(string reason, string allowedScopes, bool typed)
    {
        return ImmutableDictionary<string, string?>.Empty
            .Add("Reason", reason)
            .Add("AllowedScopes", allowedScopes)
            .Add("TypedAttribute", typed ? "true" : "false");
    }
}
