namespace PandaBear.CI.Tool;

/// <summary>The changed-code coverage policy of <c>coverage verify</c> and the symbols that <c>coverage record-symbols</c> records.</summary>
/// <param name="ThresholdPercent">The least share, from 0 to 100, of changed executable lines and conditions that the tests must cover.</param>
/// <param name="Include">Path patterns of the source files whose changed lines count.</param>
/// <param name="Exclude">Path patterns of included source files whose changed lines do not count.</param>
/// <param name="SymbolDirectories">Repository-relative directories searched for the Release portable PDBs of the build.</param>
internal sealed record CoverageConfiguration(int ThresholdPercent, string[] Include, string[] Exclude, string[] SymbolDirectories);
