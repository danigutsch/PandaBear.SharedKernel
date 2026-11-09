using PandaBear.Results;

namespace PandaBear.Secrets.Tool;

/// <summary>Implements <c>recipients list</c> and <c>recipients check</c>. Both read the file's metadata and start no process.</summary>
internal static class RecipientsCommand
{
    /// <summary>Prints the key holders of <paramref name="file"/>, one per line.</summary>
    /// <exception cref="InvalidOperationException">The path is invalid or the file's recipients cannot be read.</exception>
    public static async Task<int> List(string root, string file, TextWriter output, CancellationToken cancellationToken)
    {
        var (_, holders) = await Read(root, file, cancellationToken).ConfigureAwait(false);
        foreach (var recipient in holders.Age)
        {
            await EscapedOutput.Line(output, $"age: {recipient}", cancellationToken).ConfigureAwait(false);
        }

        foreach (var other in holders.OtherKeyTypes)
        {
            await EscapedOutput.Line(output, $"other key holder: {other}", cancellationToken).ConfigureAwait(false);
        }

        return ExitCodes.Success;
    }

    /// <summary>Checks <paramref name="file"/> against the first creation rule that matches it.</summary>
    /// <exception cref="InvalidOperationException">The configuration or path is invalid, no rule matches, or the file's recipients cannot be read.</exception>
    public static async Task<int> Check(string root, string file, TextWriter output, CancellationToken cancellationToken)
    {
        var rules = await SecretsConfigurationFile.Read(root, cancellationToken).ConfigureAwait(false);
        var (path, holders) = await Read(root, file, cancellationToken).ConfigureAwait(false);
        var rule = rules.FirstMatch(path) ?? throw new InvalidOperationException("No creation rule matches this file.");
        var verification = RecipientVerification.Compare(holders, rule);
        await Report(output, path.Value, verification, cancellationToken).ConfigureAwait(false);
        return verification.IsExact ? ExitCodes.Success : ExitCodes.Differences;
    }

    /// <summary>Writes the lines that say how a file differs from its rule, or a line that says it does not.</summary>
    public static async Task Report(TextWriter output, string path, RecipientVerification verification, CancellationToken cancellationToken)
    {
        if (verification.IsExact)
        {
            await EscapedOutput.Line(output, $"{path}: ok", cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var missing in verification.Missing)
        {
            await EscapedOutput.Line(output, $"{path}: missing recipient {missing}", cancellationToken).ConfigureAwait(false);
        }

        foreach (var unexpected in verification.Unexpected)
        {
            await EscapedOutput.Line(output, $"{path}: unexpected recipient {unexpected}", cancellationToken).ConfigureAwait(false);
        }

        foreach (var other in verification.OtherKeyTypes)
        {
            await EscapedOutput.Line(output, $"{path}: non-age key holder {other}", cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<(EncryptedFilePath Path, SopsFileRecipients Holders)> Read(string root, string file, CancellationToken cancellationToken)
    {
        var path = EncryptedFilePath.Create(file);
        if (path.IsFailure)
        {
            throw new InvalidOperationException(path.ErrorDetails?.Detail ?? "The file path is invalid.");
        }

        var resolved = path.Value.ResolveUnder(root);
        if (resolved.IsFailure)
        {
            throw new InvalidOperationException(resolved.ErrorDetails?.Detail ?? "The file path cannot be resolved under the root.");
        }

        Result<SopsFileRecipients> holders = await SopsFileReader.ReadRecipients(resolved.Value, cancellationToken).ConfigureAwait(false);
        return holders.IsFailure
            ? throw new InvalidOperationException($"{path.Value}: {holders.ErrorDetails?.Detail}")
            : (path.Value, holders.Value);
    }
}
