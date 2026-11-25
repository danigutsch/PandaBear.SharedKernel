namespace SharedKernel.Tabular;

/// <summary>Matches source columns to caller-supplied canonical names and applies those names without reparsing rows.</summary>
public static class CsvHeaderMapping
{
    /// <summary>Selects a source for each target, preserving the order of the targets.</summary>
    /// <param name="sourceHeaders">The source column names, in document order.</param>
    /// <param name="targetHeaders">The distinct canonical names.</param>
    /// <param name="overrides">
    /// Optional choices keyed by canonical name, using the dictionary's own key comparer.
    /// A null value clears the automatic match. A specified source name that is absent also leaves the target unmatched.
    /// </param>
    /// <param name="comparer">The comparer for header values. The default is ordinal case-insensitive.</param>
    /// <returns>The target names and the original spellings of their first matching source names, when present.</returns>
    /// <exception cref="ArgumentNullException">A required collection or one of its members is null.</exception>
    /// <exception cref="ArgumentException">A target name is blank or duplicated.</exception>
    public static IReadOnlyList<(string TargetHeader, string? SourceHeader)> Match(
        IReadOnlyList<string> sourceHeaders,
        IReadOnlyList<string> targetHeaders,
        IReadOnlyDictionary<string, string?>? overrides = null,
        IEqualityComparer<string>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(sourceHeaders);
        ArgumentNullException.ThrowIfNull(targetHeaders);
        comparer ??= StringComparer.OrdinalIgnoreCase;

        var sources = new Dictionary<string, string>(comparer);
        foreach (var source in sourceHeaders)
        {
            ArgumentNullException.ThrowIfNull(source, nameof(sourceHeaders));
            sources.TryAdd(source, source);
        }

        var targets = new HashSet<string>(comparer);
        var matches = new (string TargetHeader, string? SourceHeader)[targetHeaders.Count];
        for (var index = 0; index < targetHeaders.Count; index++)
        {
            var target = targetHeaders[index];
            AddTarget(targets, target, nameof(targetHeaders));
            var requestedSource = overrides is not null && overrides.TryGetValue(target, out var choice) ? choice : target;
            matches[index] = (target, requestedSource is null ? null : sources.GetValueOrDefault(requestedSource));
        }

        return Array.AsReadOnly(matches);
    }

    /// <summary>Applies the chosen canonical names in source column order, retaining the values and physical line numbers of all rows.</summary>
    /// <param name="document">The parsed or constructed CSV document.</param>
    /// <param name="matches">
    /// The canonical names and selected source names. If more than one target selects the same source, the last target wins.
    /// When multiple source columns compare equal, the first is selected.
    /// </param>
    /// <param name="renameUnselectedTarget">
    /// Optional rename policy for an unselected source whose header compares equal to a canonical name. All other unselected
    /// headers stay as given.
    /// </param>
    /// <param name="comparer">The same header comparer used to match the columns. The default is ordinal case-insensitive.</param>
    /// <returns>The document with the renamed headers, or the original instance when no name changed.</returns>
    /// <exception cref="ArgumentNullException">A required argument or target name is null, or the rename policy returns null.</exception>
    /// <exception cref="ArgumentException">A target name is blank or duplicated.</exception>
    public static CsvDocument Remap(
        CsvDocument document,
        IReadOnlyList<(string TargetHeader, string? SourceHeader)> matches,
        Func<string, string>? renameUnselectedTarget = null,
        IEqualityComparer<string>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(matches);
        comparer ??= StringComparer.OrdinalIgnoreCase;

        var targets = new HashSet<string>(comparer);
        var targetsBySource = new Dictionary<string, string>(comparer);
        foreach (var (target, source) in matches)
        {
            AddTarget(targets, target, nameof(matches));
            if (source is not null)
            {
                targetsBySource[source] = target;
            }
        }

        var headers = new string[document.Headers.Count];
        for (var index = 0; index < headers.Length; index++)
        {
            var original = document.Headers[index];
            if (targetsBySource.Remove(original, out var target))
            {
                headers[index] = target;
            }
            else if (renameUnselectedTarget is not null && targets.Contains(original))
            {
                var replacement = renameUnselectedTarget(original);
                ArgumentNullException.ThrowIfNull(replacement, nameof(renameUnselectedTarget));
                headers[index] = replacement;
            }
            else
            {
                headers[index] = original;
            }
        }

        return document.Headers.SequenceEqual(headers, StringComparer.Ordinal) ? document : document.WithHeaders(headers);
    }

    private static void AddTarget(HashSet<string> targets, string target, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target, parameterName);
        if (!targets.Add(target))
        {
            throw new ArgumentException("Canonical header names must be distinct.", parameterName);
        }
    }
}
