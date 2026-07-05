using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Testing.Roslyn;

/// <summary>
/// An <see cref="AnalyzerConfigOptionsProvider"/> backed by an in-memory dictionary, suitable for use in Roslyn analyzer and generator tests.
/// </summary>
public sealed class TestAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string>? globalOptions)
    : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigOptions _global = new TestAnalyzerConfigOptions(globalOptions);

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GlobalOptions => _global;

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => TestAnalyzerConfigOptions.Empty;

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => TestAnalyzerConfigOptions.Empty;
}

/// <summary>
/// An <see cref="AnalyzerConfigOptions"/> backed by an in-memory dictionary, suitable for use in Roslyn analyzer and generator tests.
/// </summary>
public sealed class TestAnalyzerConfigOptions(ImmutableDictionary<string, string>? values)
    : AnalyzerConfigOptions
{
    /// <summary>Gets an empty <see cref="TestAnalyzerConfigOptions"/> instance.</summary>
    public static readonly TestAnalyzerConfigOptions Empty = new(null);

    private readonly ImmutableDictionary<string, string> _options = values ?? ImmutableDictionary<string, string>.Empty;

    /// <inheritdoc/>
    public override bool TryGetValue(string key, out string value) => _options.TryGetValue(key, out value!);
}
