using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Style.Analyzers.Tests;

internal sealed class StyleTestAnalyzerConfigOptions(ImmutableDictionary<string, string>? values) : AnalyzerConfigOptions
{
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
