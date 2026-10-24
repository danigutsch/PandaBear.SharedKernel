namespace PandaBear.CI.Tool;

/// <summary>The coverage of one source line, merged over every report that names it.</summary>
/// <param name="Hit">Whether a test executed the line.</param>
/// <param name="CoveredBranches">The covered conditions of the line.</param>
/// <param name="Branches">The conditions of the line.</param>
internal readonly record struct LineCoverage(bool Hit, int CoveredBranches, int Branches);
