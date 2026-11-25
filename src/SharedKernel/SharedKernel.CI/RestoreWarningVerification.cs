using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SharedKernel.CI;

/// <summary>
/// Classifies the NuGet restore and audit warnings in a <c>dotnet restore</c> log: vulnerability advisories of every severity
/// and audits that could not run block, every other NuGet warning stays advisory. Only the warning code, package id and
/// version, and audit source name reach the output, because restore logs carry text from package feeds and the network.
/// </summary>
internal static partial class RestoreWarningVerification
{
    private const int MaxLogBytes = 16 * 1024 * 1024;

    /// <summary>
    /// The warning codes that block. NU1901, NU1902, NU1903, and NU1904 are the low, moderate, high, and critical severity
    /// advisories. NU1900 is an advisory of unknown severity or vulnerability data that could not be read, and NU1905 is an
    /// audit source without vulnerability data: an audit that could not run must not look like a clean audit.
    /// </summary>
    private static readonly string[] BlockingCodes = ["NU1900", "NU1901", "NU1902", "NU1903", "NU1904", "NU1905"];

    /// <summary>
    /// Verifies the log, appends a summary and writes a warning report when requested, and fails when a blocking finding is
    /// present. Each finding counts once, however many projects of a solution restore repeat it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The log contains a vulnerability advisory, an audit that could not run, or an expired exception.</exception>
    public static async Task Verify(string logPath, IStepSummary? summary, string? reportPath, string? exceptionsPath, CancellationToken ct)
    {
        var ledger = exceptionsPath is null ? WarningExceptionLedger.Empty : await WarningExceptionLedger.Read(exceptionsPath, ct).ConfigureAwait(false);
        var log = Encoding.UTF8.GetString(await BoundedFile.Read(logPath, MaxLogBytes, "The restore log", ct).ConfigureAwait(false));
        var findings = new SortedSet<string>(StringComparer.Ordinal);
        var blocking = new SortedSet<string>(StringComparer.Ordinal);
        var unaudited = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Warning().Matches(log))
        {
            var code = match.Groups["code"].Value;
            var text = match.Groups["text"].Value;
            var package = Package().Match(text);
            var finding = package.Success
                ? $"{code} {package.Groups["id"].Value} {package.Groups["version"].Value}"
                : code;
            if (!package.Success && code is "NU1900" or "NU1905")
            {
                finding = $"{code} {AuditSource(text)}";
                unaudited.Add(finding);
            }

            if (findings.Add(finding) && Array.IndexOf(BlockingCodes, code) >= 0)
            {
                blocking.Add(finding);
            }
        }

        var report = ledger.Classify("NuGet restore", blocking, findings.Except(blocking).ToList(), DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime));
        if (summary is not null)
        {
            await summary.Append(Render(report), ct).ConfigureAwait(false);
        }

        if (reportPath is not null)
        {
            await File.WriteAllBytesAsync(reportPath, JsonSerializer.SerializeToUtf8Bytes(report, WarningReportJsonContext.Default.WarningReport), ct).ConfigureAwait(false);
        }

        if (report.Blocking > 0)
        {
            throw new InvalidOperationException(
                "NuGet restore reported vulnerability advisories, audits that could not run, or expired exceptions:\n"
                + string.Join('\n', report.BlockingFindings.Select(static finding => "- " + finding))
                + (report.BlockingFindings.Any(unaudited.Contains)
                    ? "\nA finding that names a source means the vulnerability audit could not run: restore again once the audit source provides vulnerability data."
                    : string.Empty)
                + "\nUpdate the package or record a reviewed exception before merging.");
        }
    }

    /// <summary>
    /// Names the source of an audit that could not run: <c>source</c> followed by the name that NU1905 quotes when the name has
    /// only letters, digits, space, and <c>._:/-</c>, so credentials in a source URL never reach the output; otherwise
    /// <c>source</c> alone, as for NU1900, whose remaining text is an error message from the network or the feed.
    /// </summary>
    private static string AuditSource(string text)
    {
        var source = SourceName().Match(text);
        return source.Success ? "source " + source.Groups["name"].Value : "source";
    }

    private static string Render(WarningReport report)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"### NuGet restore and audit\n\n| Classification | Findings |\n| --- | ---: |\n| Blocking | {report.Blocking} |\n| Advisory | {report.Advisory} |\n| Accepted | {report.Accepted} |\n");
        if (report.Blocking > 0)
        {
            text.Append("\nBlocking findings:\n");
            foreach (var finding in report.BlockingFindings)
            {
                text.Append("- ").Append(finding).Append('\n');
            }
        }

        return text.Append('\n').ToString();
    }

    [GeneratedRegex(@"\bwarning (?<code>NU\d{4}): (?<text>[^\r\n]*)", RegexOptions.CultureInvariant)]
    private static partial Regex Warning();

    [GeneratedRegex(@"\APackage '(?<id>[A-Za-z0-9._-]+)' (?<version>[A-Za-z0-9.+-]+)", RegexOptions.CultureInvariant)]
    private static partial Regex Package();

    [GeneratedRegex(@"\AAudit source '(?<name>[A-Za-z0-9][A-Za-z0-9 ._:/-]{0,99})' did not provide", RegexOptions.CultureInvariant)]
    private static partial Regex SourceName();
}
