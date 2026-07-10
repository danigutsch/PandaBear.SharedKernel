namespace SharedKernel.Versioning.Tool;

internal static class ApiCompatibilityCommand
{
    private static bool ShouldTreatFailureAsReportOnly(ApiCompatibilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return ApiCompatibilityReport.ShouldTreatFailureAsReportOnly(
            new ApiCompatibilityReportOptions(
                options.Version,
                options.OutputRoot,
                options.ReleasePhase,
                options.RepoRoot,
                options.BaselineVersion,
                options.BreakingMarker));
    }

    public static async Task Run(ApiCompatibilityOptions options, TextWriter output)
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
            RepoRoot: options.RepoRoot);
        var previousPackageValidation = Environment.GetEnvironmentVariable(ApiCompatibilityEnvironmentVariables.EnablePackageValidation);
        var previousBaselineVersion = Environment.GetEnvironmentVariable(ApiCompatibilityEnvironmentVariables.BaselineVersion);

        try
        {
            Environment.SetEnvironmentVariable(ApiCompatibilityEnvironmentVariables.EnablePackageValidation, string.IsNullOrWhiteSpace(options.BaselineVersion) ? null : "true");
            Environment.SetEnvironmentVariable(ApiCompatibilityEnvironmentVariables.BaselineVersion, options.BaselineVersion);
            using var writer = new StringWriter();
            await SharedKernelPackCommand.Run(packOptions, writer).ConfigureAwait(false);
            await ApiCompatibilityReport.AppendOutput(reportPath, writer.ToString()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            await ApiCompatibilityReport.AppendOutput(reportPath, ex.Message).ConfigureAwait(false);
            if (ShouldTreatFailureAsReportOnly(options))
            {
                await output.WriteLineAsync($"API compatibility report: {reportPath}").ConfigureAwait(false);
                return;
            }

            throw;
        }
        finally
        {
            Environment.SetEnvironmentVariable(ApiCompatibilityEnvironmentVariables.EnablePackageValidation, previousPackageValidation);
            Environment.SetEnvironmentVariable(ApiCompatibilityEnvironmentVariables.BaselineVersion, previousBaselineVersion);
        }

        await output.WriteLineAsync($"API compatibility report: {reportPath}").ConfigureAwait(false);
    }
}
