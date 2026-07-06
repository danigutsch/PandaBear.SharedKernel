namespace SharedKernel.Versioning.Tool;

internal static class ApiCompatibilityCommand
{
    public static async Task Run(ApiCompatibilityOptions options, TextWriter output)
    {
        await PublicApiBaselineCommand.Run(options.RepoRoot, TextWriter.Null).ConfigureAwait(false);

        var reportDirectory = Path.Combine(ResolveOutputRoot(options), options.Version);
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, "api-compatibility-report.md");
        await File.WriteAllTextAsync(reportPath, CreateHeader(options)).ConfigureAwait(false);

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
            await AppendOutput(reportPath, writer.ToString()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            await AppendOutput(reportPath, ex.Message).ConfigureAwait(false);
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

    private static string CreateHeader(ApiCompatibilityOptions options) =>
        $"""
        # API compatibility report

        - Version: `{options.Version}`
        - Phase: `{options.ReleasePhase}`
        - Policy: alpha is report-only; beta is report-only only with `--breaking-marker`; RC/stable block breaking changes.

        """;

    private static Task AppendOutput(string reportPath, string output) =>
        File.AppendAllTextAsync(reportPath, "```text" + Environment.NewLine + output + Environment.NewLine + "```" + Environment.NewLine);

    public static bool ShouldTreatFailureAsReportOnly(ApiCompatibilityOptions options) =>
        string.Equals(options.ReleasePhase, "alpha", StringComparison.OrdinalIgnoreCase)
        || (string.Equals(options.ReleasePhase, "beta", StringComparison.OrdinalIgnoreCase) && options.BreakingMarker);

    private static string ResolveOutputRoot(ApiCompatibilityOptions options) =>
        Path.IsPathRooted(options.OutputRoot)
            ? options.OutputRoot
            : Path.Combine(options.RepoRoot, options.OutputRoot);
}
