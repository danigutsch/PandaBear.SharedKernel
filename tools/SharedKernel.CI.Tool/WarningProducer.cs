using System.Text.Json;

namespace PandaBear.CI.Tool;

/// <summary>
/// Writes the warning report of a CI step that has no log to classify, such as a linter or a test run: a failed step is one
/// blocking finding, a passed step reports its advisory count. A step that did not run is an error, never a pass.
/// </summary>
internal static class WarningProducer
{
    /// <summary>Writes the report for <paramref name="tool"/> to <paramref name="reportPath"/>, replacing it.</summary>
    /// <param name="tool">Names the producing tool.</param>
    /// <param name="outcome">The step outcome: <c>success</c> or <c>failure</c>.</param>
    /// <param name="advisory">Counts the advisory findings of a passed step.</param>
    /// <param name="reportPath">Receives the report for <c>warning-summary</c>.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <exception cref="InvalidDataException">The tool name is unsafe, or the step did not run or has an unknown outcome.</exception>
    public static async Task Write(string tool, string outcome, int advisory, string reportPath, CancellationToken ct)
    {
        if (!WarningSummary.IsToolName(tool))
        {
            throw new InvalidDataException("The tool name must start with a letter or digit and use only letters, digits, space, '.', '_' and '-'.");
        }

        var report = outcome switch
        {
            "success" => new WarningReport(tool, 0, advisory, 0, []),
            "failure" => new WarningReport(tool, 1, advisory, 0, [$"{tool} failed"]),
            _ => throw new InvalidDataException($"The outcome of '{tool}' is '{outcome}', so it did not run to a result; it is not a pass."),
        };
        await File.WriteAllBytesAsync(reportPath, JsonSerializer.SerializeToUtf8Bytes(report, WarningReportJsonContext.Default.WarningReport), ct).ConfigureAwait(false);
    }
}
