using SharedKernel.Versioning;
using SharedKernel.Versioning.Tool;

if (args is ["commit-impact", .. var messageParts])
{
    var message = string.Join(' ', messageParts);
    var commit = ConventionalCommitParser.Parse(message);
    await Console.Out.WriteLineAsync(ReleaseImpactText.ToOutputValue(commit.Impact)).ConfigureAwait(false);
    return 0;
}

if (args is ["compute", .. var computeArgs])
{
    var options = VersionToolOptions.Parse(computeArgs);
    var messages = await CommitMessageInput.ReadMessages().ConfigureAwait(false);
    var output = VersionCalculation.Calculate(
        SemanticVersion.Parse(options.BaseVersion),
        messages,
        options.Prerelease,
        options.Sha);

    await Console.Out.WriteLineAsync(VersionOutputJson.Serialize(output)).ConfigureAwait(false);
    return 0;
}

await Console.Error.WriteLineAsync("Usage: sharedkernel-version commit-impact <message> | compute --base <version> [--prerelease <label>] [--sha <sha>] < commit-messages.txt").ConfigureAwait(false);
return 2;
