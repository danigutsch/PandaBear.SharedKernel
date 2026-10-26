using System.Text.Json;

namespace PandaBear.CI.Tool;

/// <summary>
/// The reviewed exceptions of a repository, read from a JSON file that lives with the configuration it excuses. It turns the
/// raw findings of a tool into a <see cref="WarningReport"/> whose accepted count only covers unexpired, in-scope findings up to
/// each baseline.
/// </summary>
/// <param name="Exceptions">The exception records.</param>
internal sealed record WarningExceptionLedger(IReadOnlyList<WarningException> Exceptions)
{
    private const int MaxLedgerBytes = 256 * 1024;
    private const int MaxTextLength = 500;

    /// <summary>A ledger without exceptions: every finding keeps its classification.</summary>
    public static WarningExceptionLedger Empty { get; } = new([]);

    /// <summary>Reads and validates the ledger at <paramref name="path"/>.</summary>
    /// <exception cref="InvalidDataException">The ledger is empty, malformed, or holds an invalid record.</exception>
    public static async Task<WarningExceptionLedger> Read(string path, CancellationToken ct)
    {
        var bytes = await BoundedFile.Read(path, MaxLedgerBytes, "The warning exception ledger", ct).ConfigureAwait(false);
        var ledger = JsonSerializer.Deserialize(bytes, WarningExceptionJsonContext.Default.WarningExceptionLedger)
            ?? throw new InvalidDataException("The warning exception ledger is empty.");
        if (ledger.Exceptions.Any(static exception => !IsValid(exception)))
        {
            throw new InvalidDataException(
                "A warning exception needs a valid tool, rule, and scope, a baseline of at least 1, and a rationale and owner of at most "
                + $"{MaxTextLength} characters without control characters.");
        }

        return ledger;
    }

    /// <summary>
    /// Classifies <paramref name="blocking"/> and <paramref name="advisory"/> findings of <paramref name="tool"/>. An unexpired
    /// exception accepts up to its baseline of matching findings, blocking ones first; an expired exception accepts nothing
    /// and is reported as a blocking finding so it gets renewed or removed.
    /// </summary>
    public WarningReport Classify(string tool, IReadOnlyCollection<string> blocking, IReadOnlyCollection<string> advisory, DateOnly today)
    {
        var remainingBlocking = blocking.ToList();
        var remainingAdvisory = advisory.ToList();
        List<string> expired = [];
        var accepted = 0;
        foreach (var exception in Exceptions.Where(exception => string.Equals(exception.Tool, tool, StringComparison.Ordinal)))
        {
            if (exception.Expires < today)
            {
                expired.Add($"expired {exception.Rule} {exception.Scope}");
                continue;
            }

            var capacity = exception.Baseline;
            capacity -= Accept(remainingBlocking, exception, capacity);
            capacity -= Accept(remainingAdvisory, exception, capacity);
            accepted += exception.Baseline - capacity;
        }

        return new WarningReport(tool, remainingBlocking.Count + expired.Count, remainingAdvisory.Count, accepted, [.. remainingBlocking, .. expired]);
    }

    private static int Accept(List<string> findings, WarningException exception, int capacity)
    {
        var accepted = 0;
        for (var index = 0; index < findings.Count && accepted < capacity;)
        {
            if (Covers(exception, findings[index]))
            {
                findings.RemoveAt(index);
                accepted++;
            }
            else
            {
                index++;
            }
        }

        return accepted;
    }

    private static bool Covers(WarningException exception, string finding)
    {
        var separator = finding.IndexOf(' ', StringComparison.Ordinal);
        if (separator < 0 || !finding.AsSpan(0, separator).SequenceEqual(exception.Rule))
        {
            return false;
        }

        var subject = finding.AsSpan(separator + 1);
        return subject.StartsWith(exception.Scope, StringComparison.Ordinal)
            && (subject.Length == exception.Scope.Length || subject[exception.Scope.Length] is ' ' or '/');
    }

    private static bool IsValid(WarningException exception) =>
        WarningSummary.IsToolName(exception.Tool)
        && WarningSummary.IsFinding(exception.Rule)
        && !exception.Rule.Contains(' ', StringComparison.Ordinal)
        && WarningSummary.IsFinding(exception.Scope)
        && exception.Baseline >= 1
        && IsText(exception.Rationale)
        && IsText(exception.Owner);

    private static bool IsText(string text) =>
        !string.IsNullOrWhiteSpace(text) && text.Length <= MaxTextLength && !text.Any(char.IsControl);
}
