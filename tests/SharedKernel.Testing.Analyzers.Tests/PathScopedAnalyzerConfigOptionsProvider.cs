using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SharedKernel.Testing.Roslyn;

namespace SharedKernel.Testing.Analyzers.Tests;

/// <summary>Analyzer config options that apply to the files under one folder only, like an <c>.editorconfig</c> section.</summary>
/// <param name="folder">Path prefix of the files the options apply to.</param>
/// <param name="folderOptions">Options for files under the folder.</param>
internal sealed class PathScopedAnalyzerConfigOptionsProvider(string folder, ImmutableDictionary<string, string> folderOptions)
    : AnalyzerConfigOptionsProvider
{
    private readonly TestAnalyzerConfigOptions _folderOptions = new(folderOptions);

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GlobalOptions => TestAnalyzerConfigOptions.Empty;

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return tree.FilePath.StartsWith(folder, StringComparison.Ordinal) ? _folderOptions : TestAnalyzerConfigOptions.Empty;
    }

    /// <inheritdoc/>
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        return TestAnalyzerConfigOptions.Empty;
    }
}
