namespace SharedKernel.Versioning.Tool;

internal static class VersioningToolApplication
{
    public static async Task<int> Run(string[] args, TextReader input, TextWriter output, TextWriter error)
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

        await error.WriteLineAsync("Usage: sharedkernel-version commit-impact <message> | compute --base <version> [--prerelease <label>] [--sha <sha>] < commit-messages.txt").ConfigureAwait(false);
        return 2;
    }
}
