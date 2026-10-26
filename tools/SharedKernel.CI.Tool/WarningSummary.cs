using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PandaBear.CI.Tool;

/// <summary>
/// Renders the warning reports of several CI tools as one table of blocking, advisory, and accepted totals. The reports are
/// files other steps wrote, so a tool name or finding that could start a table cell, line, or link is rejected.
/// </summary>
internal static partial class WarningSummary
{
    private const int MaxReportBytes = 1024 * 1024;

    /// <summary>Writes the summary to <paramref name="summaryPath"/>, appending, or to <paramref name="output"/> without one.</summary>
    /// <exception cref="InvalidDataException">A report is invalid or repeats the tool of another report.</exception>
    public static async Task Write(IReadOnlyList<string> reportPaths, string? summaryPath, TextWriter output, CancellationToken ct)
    {
        List<WarningReport> reports = [];
        foreach (var path in reportPaths)
        {
            reports.Add(await Read(path, ct).ConfigureAwait(false));
        }

        var duplicate = reports.GroupBy(static report => report.Tool, StringComparer.Ordinal).FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException($"Two warning reports name the tool '{duplicate.Key}'.");
        }

        var text = Render(reports);
        if (summaryPath is null)
        {
            await output.WriteAsync(text.AsMemory(), ct).ConfigureAwait(false);
            return;
        }

        await File.AppendAllTextAsync(summaryPath, text, ct).ConfigureAwait(false);
    }

    private static async Task<WarningReport> Read(string path, CancellationToken ct)
    {
        var bytes = await BoundedFile.Read(path, MaxReportBytes, "A warning report", ct).ConfigureAwait(false);
        var report = JsonSerializer.Deserialize(bytes, WarningReportJsonContext.Default.WarningReport)
            ?? throw new InvalidDataException("A warning report is empty.");
        if (!ToolName().IsMatch(report.Tool)
            || report.Blocking < 0
            || report.Advisory < 0
            || report.Accepted < 0
            || report.BlockingFindings.Any(static finding => !Finding().IsMatch(finding)))
        {
            throw new InvalidDataException("A warning report has an invalid tool name, a negative count, or an invalid finding.");
        }

        return report;
    }

    private static string Render(List<WarningReport> reports)
    {
        var text = new StringBuilder("### CI warnings\n\n| Tool | Blocking | Advisory | Accepted |\n| --- | ---: | ---: | ---: |\n");
        foreach (var report in reports.OrderBy(static report => report.Tool, StringComparer.Ordinal))
        {
            text.Append(CultureInfo.InvariantCulture, $"| {report.Tool} | {report.Blocking} | {report.Advisory} | {report.Accepted} |\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"| **Total** | {reports.Sum(static report => report.Blocking)} | {reports.Sum(static report => report.Advisory)} | {reports.Sum(static report => report.Accepted)} |\n");
        var findings = reports.OrderBy(static report => report.Tool, StringComparer.Ordinal)
            .SelectMany(static report => report.BlockingFindings.Select(finding => $"- {report.Tool}: {finding}\n"))
            .ToArray();
        if (findings.Length > 0)
        {
            text.Append("\nBlocking findings:\n").AppendJoin(string.Empty, findings);
        }

        return text.Append('\n').ToString();
    }

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9 ._-]{0,39}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ToolName();

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9 ._:@+-]{0,199}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Finding();
}
