namespace PandaBear.Tabular;

/// <summary>Finds later rows sharing a caller-defined key.</summary>
public static class DuplicateRowScanner
{
    /// <summary>Finds the one-based document line numbers of later duplicate keys.</summary>
    /// <param name="document">The document whose first line contains headers.</param>
    /// <param name="keySelector">Selects a comparison key, or null to skip a row.</param>
    /// <param name="comparer">The key comparer; ordinal comparison is used when null.</param>
    /// <returns>Duplicate line numbers in document order, excluding the first row for each key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> or <paramref name="keySelector"/> is null.</exception>
    /// <remarks>Normalization and empty-key policy belong to the caller. Non-null empty keys participate in comparison.</remarks>
    public static IReadOnlyList<int> FindDuplicateLineNumbers(
        CsvDocument document,
        Func<CsvRow, string?> keySelector,
        IEqualityComparer<string>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(keySelector);

        var seenKeys = new HashSet<string>(comparer ?? StringComparer.Ordinal);
        var duplicateLineNumbers = new List<int>();
        foreach (var (row, index) in document.Rows.Select((row, index) => (row, index)))
        {
            var key = keySelector(row);
            if (key is not null && !seenKeys.Add(key))
            {
                duplicateLineNumbers.Add(index + 2);
            }
        }

        return duplicateLineNumbers;
    }
}
