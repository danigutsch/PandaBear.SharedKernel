namespace SharedKernel.CI;

/// <summary>The changed-code coverage policy and the symbols that <c>coverage record-symbols</c> records.</summary>
/// <param name="ThresholdPercent">The least share, from 0 to 100, of changed executable lines and conditions that the tests must cover.</param>
/// <param name="Include">Path patterns of the source files whose changed lines count.</param>
/// <param name="Exclude">Path patterns of included source files whose changed lines do not count.</param>
/// <param name="SymbolDirectories">Repository-relative directories searched for the Release portable PDBs of the build.</param>
public sealed record CoverageConfiguration(int ThresholdPercent, IReadOnlyList<string> Include, IReadOnlyList<string> Exclude, IReadOnlyList<string> SymbolDirectories)
{
    /// <summary>Returns whether the coverage policy counts the repository-relative source path.</summary>
    internal bool Includes(string path) =>
        ProjectSelector.Match(Include, path) is not null && ProjectSelector.Match(Exclude, path) is null;
}
