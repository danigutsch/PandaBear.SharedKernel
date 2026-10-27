namespace PandaBear.Versioning.Tool;

internal static class ApiCompatibilityCommand
{
    public static async Task Run(ApiCompatibilityOptions options, TextWriter output, CancellationToken ct)
    {
        await PublicApiBaselineCommand.Run(options.RepoRoot, TextWriter.Null).ConfigureAwait(false);

        var reportOptions = new ApiCompatibilityReportOptions(
            options.Version,
            options.OutputRoot,
            options.ReleasePhase,
            options.RepoRoot,
            options.BaselineVersion,
            options.BreakingMarker);
        var reportPath = await ApiCompatibilityReport.Initialize(reportOptions).ConfigureAwait(false);

        var packOptions = new PackSharedKernelOptions(
            options.Version,
            Path.Combine(options.OutputRoot, "packages"),
            VerifyRestore: string.IsNullOrWhiteSpace(options.BaselineVersion),
            RepoRoot: options.RepoRoot,
            PackageValidationBaselineVersion: options.BaselineVersion);

        try
        {
            using var writer = new StringWriter();
            await SharedKernelPackCommand.Run(packOptions, writer, ct).ConfigureAwait(false);
            await ApiCompatibilityReport.AppendOutput(reportPath, writer.ToString()).ConfigureAwait(false);
        }
        catch (Exception ex) when ((ex is ArgumentException or InvalidOperationException) && !ct.IsCancellationRequested)
        {
            await ApiCompatibilityReport.AppendOutput(reportPath, ex.Message).ConfigureAwait(false);
            if (!ApiCompatibilityReport.ShouldTreatFailureAsReportOnly(reportOptions))
            {
                throw;
            }
        }

        await output.WriteLineAsync($"API compatibility report: {reportPath}").ConfigureAwait(false);
    }
}
