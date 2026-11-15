namespace PandaBear.CI;

/// <summary>
/// Classifies, records, and summarizes the warnings of CI steps: the NuGet advisories of a restore log, the counts of a
/// step without a log, and one table of blocking, advisory, and accepted totals across the reports of several tools.
/// </summary>
public static class WarningReports
{
    /// <summary>
    /// Fails when a <c>dotnet restore</c> log reports a NuGet vulnerability advisory of any severity, or an audit that could
    /// not run (NU1900, NU1905), that no exception covers; other NuGet warnings stay advisory.
    /// </summary>
    /// <param name="logPath">The restore log.</param>
    /// <param name="summary">The step summary that receives the classification, or <see langword="null"/>.</param>
    /// <param name="reportPath">The warning report file to write, or <see langword="null"/>.</param>
    /// <param name="exceptionsPath">The warning exception ledger, or <see langword="null"/>.</param>
    /// <param name="ct">Cancels reading and writing.</param>
    /// <exception cref="InvalidOperationException">The log reports a blocking advisory or an audit that could not run, or an exception expired.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static Task VerifyRestore(string logPath, IStepSummary? summary, string? reportPath, string? exceptionsPath, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(logPath);
        return RestoreWarningVerification.Verify(logPath, summary, reportPath, exceptionsPath, ct);
    }

    /// <summary>Writes the warning report of a step without a log to classify, such as a linter or a test run.</summary>
    /// <param name="tool">The name of the producing tool, such as <c>Markdown lint</c>.</param>
    /// <param name="outcome">The outcome of the step: <c>success</c> or <c>failure</c>; anything else fails the report.</param>
    /// <param name="advisory">The number of advisory findings.</param>
    /// <param name="reportPath">The report file, replaced if it exists.</param>
    /// <param name="ct">Cancels writing.</param>
    /// <exception cref="InvalidDataException">The tool name is unsafe, or the step did not run or has an unknown outcome.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static Task Write(string tool, string outcome, int advisory, string reportPath, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(reportPath);
        return WarningProducer.Write(tool, outcome, advisory, reportPath, ct);
    }

    /// <summary>Renders the reports as one table with the blocking findings.</summary>
    /// <param name="reportPaths">The warning report files, one per tool.</param>
    /// <param name="summary">The step summary that receives the table, or <see langword="null"/> to write it to <paramref name="output"/>.</param>
    /// <param name="output">Receives the table without a summary.</param>
    /// <param name="ct">Cancels reading and writing.</param>
    /// <exception cref="InvalidDataException">A report is not valid, or two reports name the same tool.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static Task Summarize(IReadOnlyList<string> reportPaths, IStepSummary? summary, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reportPaths);
        ArgumentNullException.ThrowIfNull(output);
        return WarningSummary.Write(reportPaths, summary, output, ct);
    }
}
