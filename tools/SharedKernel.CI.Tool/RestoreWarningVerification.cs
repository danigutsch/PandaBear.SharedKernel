using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PandaBear.CI.Tool;

/// <summary>
/// Classifies the NuGet restore and audit warnings in a <c>dotnet restore</c> log: high and critical vulnerability advisories
/// block, every other NuGet warning stays advisory. Only the warning code, package id, and package version reach the output,
/// because restore logs carry text from package feeds.
/// </summary>
internal static partial class RestoreWarningVerification
{
    private const int MaxLogBytes = 16 * 1024 * 1024;

    // NU1903 is a high and NU1904 a critical severity advisory; NU1901 and NU1902 are low and moderate.
    private static readonly string[] BlockingCodes = ["NU1903", "NU1904"];

    /// <summary>Verifies the log, appends a summary and writes a warning report when requested, and fails when a blocking advisory is present.</summary>
    /// <exception cref="InvalidOperationException">The log contains a high or critical vulnerability advisory.</exception>
    public static async Task Verify(string logPath, string? summaryPath, string? reportPath, CancellationToken ct)
    {
        var log = Encoding.UTF8.GetString(await BoundedFile.Read(logPath, MaxLogBytes, "The restore log", ct).ConfigureAwait(false));
        var findings = new SortedSet<string>(StringComparer.Ordinal);
        var blocking = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match match in Warning().Matches(log))
        {
            var code = match.Groups["code"].Value;
            var package = Package().Match(match.Groups["text"].Value);
            var finding = package.Success
                ? $"{code} {package.Groups["id"].Value} {package.Groups["version"].Value}"
                : code;

            // The same package repeats once per project in a solution restore.
            if (findings.Add(finding) && Array.IndexOf(BlockingCodes, code) >= 0)
            {
                blocking.Add(finding);
            }
        }

        if (summaryPath is not null)
        {
            await File.AppendAllTextAsync(summaryPath, Render(findings.Count - blocking.Count, blocking), ct).ConfigureAwait(false);
        }

        if (reportPath is not null)
        {
            var report = new WarningReport("NuGet restore", blocking.Count, findings.Count - blocking.Count, 0, [.. blocking]);
            await File.WriteAllBytesAsync(reportPath, JsonSerializer.SerializeToUtf8Bytes(report, WarningReportJsonContext.Default.WarningReport), ct).ConfigureAwait(false);
        }

        if (blocking.Count > 0)
        {
            throw new InvalidOperationException(
                "NuGet restore reported high or critical vulnerability advisories:\n"
                + string.Join('\n', blocking.Select(static finding => "- " + finding))
                + "\nUpdate the package or record a reviewed exception before merging.");
        }
    }

    private static string Render(int advisory, SortedSet<string> blocking)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"### NuGet restore and audit\n\n| Classification | Findings |\n| --- | ---: |\n| Blocking | {blocking.Count} |\n| Advisory | {advisory} |\n");
        if (blocking.Count > 0)
        {
            text.Append("\nBlocking findings:\n");
            foreach (var finding in blocking)
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
}
