using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Style.Analyzers.Tests;

/// <summary>
/// Analyzer config options backed by an in-memory dictionary for analyzer tests.
/// </summary>
/// <param name="values">The option keys and values, or null for no options.</param>
internal sealed class StyleTestAnalyzerConfigOptions(ImmutableDictionary<string, string>? values) : AnalyzerConfigOptions
{
    /// <summary>Shared instance with no options.</summary>
    public static readonly StyleTestAnalyzerConfigOptions Empty = new(null);

    private readonly ImmutableDictionary<string, string> options = values ?? ImmutableDictionary<string, string>.Empty;

    public override bool TryGetValue(string key, out string value)
    {
        if (options.TryGetValue(key, out var candidateValue))
        {
            value = candidateValue;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
