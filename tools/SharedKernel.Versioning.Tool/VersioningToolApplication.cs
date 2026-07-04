using System.Reflection;

namespace SharedKernel.Versioning.Tool;

internal static class VersioningToolApplication
{
    public static async Task<int> Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        if (args is [] or ["--help"] or ["-h"] or ["commit-impact", "--help"] or ["commit-impact", "-h"] or ["compute", "--help"] or ["compute", "-h"])
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

        Commands:
          commit-impact   Prints release impact for one Conventional Commit message.
          compute         Prints version output JSON from null-separated commit messages on standard input.

        Options:
          --base <version>        Base semantic version for compute.
          --prerelease <label>    Optional prerelease label for compute.
          --sha <sha>             Optional source revision for informational version metadata.
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
