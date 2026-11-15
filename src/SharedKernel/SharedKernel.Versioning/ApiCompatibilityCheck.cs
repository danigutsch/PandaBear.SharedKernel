namespace PandaBear.Versioning;

/// <summary>Packs SharedKernel with package validation and writes an API compatibility report.</summary>
internal static class ApiCompatibilityCheck
{
    /// <summary>
    /// Checks the public API baselines, packs against the optional baseline version, appends the output to the report,
    /// and rethrows pack failures unless the release phase treats them as report-only.
    /// </summary>
    public static async Task Run(ExternalCommands commands, ApiCompatibilityReportOptions options, TextWriter output, CancellationToken ct)
    {
        PublicApiBaselineChecker.EnsureBaselinesPresent(options.RepoRoot);
        var reportPath = await ApiCompatibilityReport.Initialize(options).ConfigureAwait(false);
        var packageOptions = new SharedKernelPackageOptions(options.Version, Path.Combine(options.OutputRoot, "packages"), options.RepoRoot);
        try
        {
            using var writer = new StringWriter();
            await SharedKernelPack.Run(
                commands,
                packageOptions,
                verifyRestore: string.IsNullOrWhiteSpace(options.BaselineVersion),
                options.BaselineVersion,
                writer,
                ct).ConfigureAwait(false);
            await ApiCompatibilityReport.AppendOutput(reportPath, writer.ToString()).ConfigureAwait(false);
        }
        catch (Exception ex) when ((ex is ArgumentException or InvalidOperationException) && !ct.IsCancellationRequested)
        {
            await ApiCompatibilityReport.AppendOutput(reportPath, ex.Message).ConfigureAwait(false);
            if (!ApiCompatibilityReport.ShouldTreatFailureAsReportOnly(options))
            {
                throw;
            }
        }

        await output.WriteLineAsync($"API compatibility report: {reportPath}").ConfigureAwait(false);
    }
}
