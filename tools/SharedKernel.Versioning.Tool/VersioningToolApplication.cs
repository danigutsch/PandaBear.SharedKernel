using System.Reflection;

namespace SharedKernel.Versioning.Tool;

internal static class VersioningToolApplication
{
    public static async Task<int> Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        if (args is [] or ["--help"] or ["-h"] or ["commit-impact", "--help"] or ["commit-impact", "-h"] or ["compute", "--help"] or ["compute", "-h"] or ["prepare-release", "--help"] or ["prepare-release", "-h"])
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

            if (args is ["prepare-release", .. var releaseArgs])
            {
                var options = PrepareReleaseOptions.Parse(releaseArgs);
                await ReleaseArtifactWriter.Write(options, input).ConfigureAwait(false);
                await output.WriteLineAsync($"Release prep artifacts: {options.OutputDirectory}").ConfigureAwait(false);
                return 0;
            }
        }
        catch (ArgumentException ex)
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
          sharedkernel-version prepare-release --version <semver> --package-dir <path> [--output-dir <path>] [--source-tag <tag>] [--release-impact <impact>] [--sha <sha>] < changes.txt

        Commands:
          commit-impact   Prints release impact for one Conventional Commit message.
          compute         Prints version output JSON from null-separated commit messages on standard input.
          prepare-release Writes release notes, changelog, and package manifest artifacts.

        Options:
          --base <version>        Base semantic version for compute.
          --prerelease <label>    Optional prerelease label for compute.
          --sha <sha>             Optional source revision for informational version metadata.
          --version <semver>      Release version for prepare-release.
          --package-dir <path>    Package artifact directory for prepare-release.
          --output-dir <path>     Output directory for prepare-release artifacts.
          --source-tag <tag>      Previous release tag for prepare-release notes.
          --release-impact <text> Release impact value for prepare-release notes.
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
}
