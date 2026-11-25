namespace PandaBear.CI;

/// <summary>The coverage of one source line, merged over every report that names it.</summary>
/// <param name="Hit">Whether a test executed the line.</param>
/// <param name="CoveredBranches">The covered conditions of the line.</param>
/// <param name="Branches">The conditions of the line.</param>
internal readonly record struct LineCoverage(bool Hit, int CoveredBranches, int Branches)
{
    /// <summary>Counts executable lines and conditions at the changed line numbers that the reports list.</summary>
    internal static (long HitLines, long Lines, long HitConditions, long Conditions) CountChanged(
        IEnumerable<int> changedLines, IReadOnlyDictionary<int, LineCoverage> coverage)
    {
        long hitLines = 0;
        long lines = 0;
        long hitConditions = 0;
        long conditions = 0;
        foreach (var number in changedLines)
        {
            if (coverage.TryGetValue(number, out var line))
            {
                lines++;
                hitLines += line.Hit ? 1 : 0;
                conditions += line.Branches;
                hitConditions += line.CoveredBranches;
            }
        }

        return (hitLines, lines, hitConditions, conditions);
    }
}
