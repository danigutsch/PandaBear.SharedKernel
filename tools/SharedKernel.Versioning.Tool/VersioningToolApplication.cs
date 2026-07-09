using System.Reflection;

namespace SharedKernel.Versioning.Tool;

internal static class VersioningToolApplication
{
    public static async Task<int> Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        if (args is [] or ["--help"] or ["-h"] or ["commit-impact", "--help"] or ["commit-impact", "-h"] or ["compute", "--help"] or ["compute", "-h"] or ["calculate-release", "--help"] or ["calculate-release", "-h"] or ["pack-sharedkernel", "--help"] or ["pack-sharedkernel", "-h"] or ["prepare-release", "--help"] or ["prepare-release", "-h"] or ["check-public-api-baselines", "--help"] or ["check-public-api-baselines", "-h"] or ["validate-package-metadata", "--help"] or ["validate-package-metadata", "-h"] or ["has-breaking-change-marker", "--help"] or ["has-breaking-change-marker", "-h"] or ["api-compatibility", "--help"] or ["api-compatibility", "-h"])
        {
            await output.WriteLineAsync(Usage).ConfigureAwait(false);
            return 0;
        }

        if (args is ["--version"])
        {
            await output.WriteLineAsync(GetVersion()).ConfigureAwait(false);
            return 0;
        }

        try
        {
            if (args is ["commit-impact", .. var messageParts])
            {
                var message = string.Join(' ', messageParts);
                var commit = ConventionalCommitParser.Parse(message);
                await output.WriteLineAsync(ReleaseImpactText.ToOutputValue(commit.Impact)).ConfigureAwait(false);
                return 0;
            }

            if (args is ["compute", .. var computeArgs])
            {
                var options = VersionToolOptions.Parse(computeArgs);
                var messages = await CommitMessageInput.ReadMessages(input).ConfigureAwait(false);
                var versionOutput = VersionCalculation.Calculate(
                    SemanticVersion.Parse(options.BaseVersion),
                    messages,
                    options.Prerelease,
                    options.Sha);

                await output.WriteLineAsync(VersionOutputJson.Serialize(versionOutput)).ConfigureAwait(false);
                return 0;
            }

            if (args is ["calculate-release", .. var calculateArgs])
            {
                var options = CalculateReleaseOptions.Parse(calculateArgs);
                await ReleaseVersionCommand.Run(options, output).ConfigureAwait(false);
                return 0;
            }

            if (args is ["pack-sharedkernel", .. var packArgs])
            {
                var options = PackSharedKernelOptions.Parse(packArgs);
                await SharedKernelPackCommand.Run(options, output).ConfigureAwait(false);
                return 0;
            }

            if (args is ["prepare-release", .. var releaseArgs])
            {
                var options = PrepareReleaseOptions.Parse(releaseArgs);
                await ReleaseArtifactWriter.Write(options, input).ConfigureAwait(false);
                await output.WriteLineAsync($"Release prep artifacts: {options.OutputDirectory}").ConfigureAwait(false);
                return 0;
            }

            if (args is ["check-public-api-baselines", .. var baselineArgs])
            {
                var repoRoot = ParseRepoRoot(baselineArgs);
                await PublicApiBaselineCommand.Run(repoRoot, output).ConfigureAwait(false);
                return 0;
            }

            if (args is ["validate-package-metadata", .. var metadataArgs])
            {
                var repoRoot = ParseRepoRoot(metadataArgs);
                PackageMetadataValidationCommand.Run(repoRoot, output);
                return 0;
            }

            if (args is ["has-breaking-change-marker", var range, .. var markerArgs])
            {
                var repoRoot = ParseRepoRoot(markerArgs);
                await BreakingChangeMarkerCommand.Run(range, repoRoot, output).ConfigureAwait(false);
                return 0;
            }

            if (args is ["api-compatibility", .. var apiCompatibilityArgs])
            {
                var options = ApiCompatibilityOptions.Parse(apiCompatibilityArgs);
                await ApiCompatibilityCommand.Run(options, output).ConfigureAwait(false);
                return 0;
            }
        }
        catch (ArgumentException ex)
        {
            await error.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
            return 2;
        }
        catch (InvalidOperationException ex)
        {
            await error.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
            return 2;
        }

        await error.WriteLineAsync(Usage).ConfigureAwait(false);
        return 2;
    }

    private const string Usage = """
        Usage:
          sharedkernel-version --help
          sharedkernel-version --version
          sharedkernel-version commit-impact <message>
          sharedkernel-version compute --base <version> [--prerelease <label>] [--sha <sha>] < commit-messages.txt
          sharedkernel-version calculate-release [--repo-root <path>] [--version-kind <prerelease|stable>] [--run-number <number>] [--sha <sha>] [--github-output <path>] [--github-summary <path>]
          sharedkernel-version pack-sharedkernel [--version <semver>] [--assembly-version <version>] [--file-version <version>] [--informational-version <version>] [--output-root <path>] [--repo-root <path>] [--skip-restore-check]
          sharedkernel-version prepare-release --version <semver> --package-dir <path> [--output-dir <path>] [--repo-root <path>] [--source-tag <tag>] [--release-impact <impact>] [--sha <sha>] < changes.txt
          sharedkernel-version check-public-api-baselines [--repo-root <path>]
          sharedkernel-version validate-package-metadata [--repo-root <path>]
          sharedkernel-version has-breaking-change-marker <git-range> [--repo-root <path>]
          sharedkernel-version api-compatibility [--version <semver>] [--release-phase <alpha|beta|rc|stable>] [--baseline-version <semver>] [--breaking-marker] [--output-root <path>] [--repo-root <path>]

        Commands:
          commit-impact   Prints release impact for one Conventional Commit message.
          compute         Prints version output JSON from null-separated commit messages on standard input.
          calculate-release Calculates release version from Git history and optionally writes GitHub outputs.
          pack-sharedkernel Packs SharedKernel packages and verifies local feed restore.
          prepare-release Writes release notes, changelog, and package manifest artifacts.
          check-public-api-baselines Checks PublicAPI baselines for SharedKernel projects.
          validate-package-metadata Validates required SharedKernel package metadata.
          has-breaking-change-marker Checks commit history for a breaking-change marker.
          api-compatibility Writes the SharedKernel package API compatibility report.

        Options:
          --base <version>        Base semantic version for compute.
          --prerelease <label>    Optional prerelease label for compute.
          --repo-root <path>      Repository root for Git-backed release and pack commands.
          --version-kind <mode>   Release version mode: prerelease or stable.
          --run-number <number>   CI run number used in prerelease labels.
          --sha <sha>             Optional source revision for informational version metadata.
          --github-output <path>  GitHub Actions output file for calculate-release.
          --github-summary <path> GitHub Actions summary file for calculate-release.
          --version <semver>      Release version for prepare-release.
          --assembly-version <v>  Assembly version for pack-sharedkernel.
          --file-version <v>      File version for pack-sharedkernel.
          --informational-version <v> Informational version for pack-sharedkernel.
          --output-root <path>    Package output root for pack-sharedkernel.
          --skip-restore-check    Skip local feed restore verification for pack-sharedkernel.
          --package-dir <path>    Package artifact directory for prepare-release.
          --output-dir <path>     Output directory for prepare-release artifacts.
          --source-tag <tag>      Previous release tag for prepare-release notes.
          --release-impact <text> Release impact value for prepare-release notes.
          --release-phase <phase> API gate phase: alpha, beta, rc, or stable.
          --baseline-version <v>  Previous package version for package validation.
          --breaking-marker       Acknowledge a breaking API diff for non-alpha phases.
          --help, -h              Print help and exit successfully.
          --version               Print version and exit successfully.

        Exit codes:
          0   Success.
          2   Invalid command, arguments, or input.
        """;

    private static string GetVersion()
    {
        var version = typeof(VersioningToolApplication).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return string.IsNullOrWhiteSpace(version) ? "unknown" : version;
    }

    private static string ParseRepoRoot(string[] args)
    {
        if (args.Length == 0)
        {
            return ".";
        }

        if (args is ["--repo-root", var repoRoot])
        {
            return repoRoot;
        }

        throw new ArgumentException("Unknown repo-root option(s): " + string.Join(' ', args) + ". Expected: [--repo-root <path>].");
    }
}
