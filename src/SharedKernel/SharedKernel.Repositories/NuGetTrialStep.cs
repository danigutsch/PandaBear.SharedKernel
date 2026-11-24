using System.Text.RegularExpressions;
using PandaBear.Processes;

namespace PandaBear.Repositories;

/// <summary>Records stable diagnostics for a trial step without saving raw SDK output.</summary>
/// <param name="Name">Name of the verification step.</param>
/// <param name="ExitCode">Exit code returned by the SDK.</param>
/// <param name="Codes">Distinct NuGet diagnostic codes.</param>
/// <param name="Advisories">Known advisories reported while evaluating the step.</param>
internal sealed partial record NuGetTrialStep(
    string Name, int ExitCode, IReadOnlyList<string> Codes, IReadOnlyList<NuGetTrialAdvisory> Advisories)
{
    /// <summary>The direct package IDs selected for an SDK update step.</summary>
    public IReadOnlyList<string> Packages { get; init; } = [];

    /// <summary>The exact central updates selected for this candidate.</summary>
    public IReadOnlyList<string> Updates { get; init; } = [];

    /// <summary>The failure codes used to distinguish a rejected candidate from an indeterminate restore.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Reports whether the SDK could not obtain vulnerability information.</summary>
    internal bool AuditUnavailable => Codes.Contains("NU1900") || Codes.Contains("NU1905");

    /// <summary>Reports an unavailable package source even when the repository tolerates the diagnostic as a warning.</summary>
    internal bool SourceUnavailable => Codes.Contains("NU1301") || Codes.Contains("NU1801");

    /// <summary>Extracts NuGet codes and advisory identifiers from the SDK diagnostics.</summary>
    internal static NuGetTrialStep Read(string name, string root, ProcessOutput process, NuGetFileApp? fileApp = null)
    {
        var lines = (process.StandardOutput + "\n" + process.StandardError).Split('\n');
        var codes = lines.SelectMany(line => CodePattern().Matches(line).Select(match => match.Groups[1].Value.ToUpperInvariant()))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var advisories = lines.Select(line => AdvisoryPattern().Match(line))
            .Where(match => match.Success)
            .Select(match => new NuGetTrialAdvisory(
                match.Groups["code"].Value, match.Groups["package"].Value, match.Groups["version"].Value,
                match.Groups["advisory"].Value, Project(root, match.Groups["project"].Value, fileApp)))
            .Distinct().OrderBy(advisory => advisory.Project, StringComparer.Ordinal)
            .ThenBy(advisory => advisory.Package, StringComparer.Ordinal)
            .ThenBy(advisory => advisory.Advisory, StringComparer.Ordinal).ToArray();
        var errors = lines.Select(line => SeverityPattern().Match(line))
            .Where(match => match.Success && string.Equals(match.Groups["severity"].Value, "error", StringComparison.OrdinalIgnoreCase))
            .Select(match => match.Groups["code"].Success ? match.Groups["code"].Value.ToUpperInvariant() : "unknown")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return new(name, process.ExitCode, codes, advisories) { Errors = errors };
    }

    private static string? Project(string root, string path, NuGetFileApp? fileApp)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var relative = Path.GetRelativePath(root, Path.GetFullPath(path, root));
        if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
        {
            return null;
        }

        var normalized = relative.Replace('\\', '/');
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return fileApp is not null && string.Equals(normalized, fileApp.VirtualProjectPath, comparison) ? fileApp.Path : normalized;
    }

    [GeneratedRegex(@"\b(?:warning|error) (NU\d{4}):", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"\b(?<severity>warning|error)(?: (?<code>[A-Z]+\d+))?\s*:", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SeverityPattern();

    [GeneratedRegex(@"(?<project>[^\r\n]*?\.(?:cs|fs|vb)proj)(?:\([^)]*\))?\s*:\s*(?:warning|error) (?<code>NU190[1-4]): (?:Warning As Error: )?Package '(?<package>[\w.-]+)' (?<version>[\w.+-]+) [^\r\n]*?(?<advisory>https://[^\s\[\],?#]+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex AdvisoryPattern();
}
