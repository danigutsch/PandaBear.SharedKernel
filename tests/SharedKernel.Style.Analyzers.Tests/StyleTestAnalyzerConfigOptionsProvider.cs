using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Style.Analyzers.Tests;

/// <summary>
/// Analyzer config options provider that serves global and per-syntax-tree options from memory and empty options for additional files.
/// </summary>
/// <param name="globalOptions">Options returned as the global options, or null for none.</param>
/// <param name="syntaxTreeOptions">Options keyed by syntax tree, or null for none.</param>
internal sealed class StyleTestAnalyzerConfigOptionsProvider(
    ImmutableDictionary<string, string>? globalOptions = null,
    ImmutableDictionary<SyntaxTree, ImmutableDictionary<string, string>>? syntaxTreeOptions = null)
    : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigOptions global = new StyleTestAnalyzerConfigOptions(globalOptions);
    private readonly ImmutableDictionary<SyntaxTree, ImmutableDictionary<string, string>> perTreeOptions = syntaxTreeOptions
        ?? ImmutableDictionary<SyntaxTree, ImmutableDictionary<string, string>>.Empty;

    public override AnalyzerConfigOptions GlobalOptions => global;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return perTreeOptions.TryGetValue(tree, out var values)
            ? new StyleTestAnalyzerConfigOptions(values)
            : StyleTestAnalyzerConfigOptions.Empty;
    }

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => StyleTestAnalyzerConfigOptions.Empty;
}
