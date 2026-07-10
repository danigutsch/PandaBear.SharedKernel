namespace SharedKernel.Versioning;

/// <summary>
/// Creates API compatibility report files and applies release-phase failure policy.
/// </summary>
public static class ApiCompatibilityReport
{
    /// <summary>
    /// Creates the report file and writes its header.
    /// </summary>
    /// <param name="options">The report options.</param>
    /// <returns>The report path.</returns>
    public static async Task<string> Initialize(ApiCompatibilityReportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var reportPath = GetReportPath(options);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? throw new ArgumentException($"Invalid report path: {reportPath}"));
        await File.WriteAllTextAsync(reportPath, CreateHeader(options)).ConfigureAwait(false);

        return reportPath;
    }

    /// <summary>
    /// Appends tool output to an API compatibility report.
    /// </summary>
    /// <param name="reportPath">The report path.</param>
    /// <param name="output">The output to append.</param>
    /// <returns>A task that completes when output is appended.</returns>
    public static Task AppendOutput(string reportPath, string output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        ArgumentNullException.ThrowIfNull(output);

        return File.AppendAllTextAsync(reportPath, "```text" + Environment.NewLine + output + Environment.NewLine + "```" + Environment.NewLine);
    }

    /// <summary>
    /// Determines whether a package validation failure should produce a report without failing the command.
    /// </summary>
    /// <param name="options">The report options.</param>
    /// <returns><see langword="true" /> when the failure is report-only.</returns>
    public static bool ShouldTreatFailureAsReportOnly(ApiCompatibilityReportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return string.Equals(options.ReleasePhase, "alpha", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(options.ReleasePhase, "beta", StringComparison.OrdinalIgnoreCase) && options.BreakingMarker);
    }

    /// <summary>
    /// Gets the API compatibility report path for the options.
    /// </summary>
    /// <param name="options">The report options.</param>
    /// <returns>The report path.</returns>
    public static string GetReportPath(ApiCompatibilityReportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Path.Combine(ResolveOutputRoot(options), options.Version, "api-compatibility-report.md");
    }

    private static string CreateHeader(ApiCompatibilityReportOptions options) =>
        $"""
        # API compatibility report

        - Version: `{options.Version}`
        - Phase: `{options.ReleasePhase}`
        - Policy: alpha is report-only; beta is report-only only with `--breaking-marker`; RC/stable block breaking changes.

        """;

    private static string ResolveOutputRoot(ApiCompatibilityReportOptions options) =>
        Path.IsPathRooted(options.OutputRoot)
            ? options.OutputRoot
            : Path.Combine(options.RepoRoot, options.OutputRoot);
}
