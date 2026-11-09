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

        foreach (var concern in holders.Concerns)
        {
            await EscapedOutput.Line(output, $"concern: {concern}", cancellationToken).ConfigureAwait(false);
        }

        return ExitCodes.Success;
    }

    /// <summary>Checks <paramref name="file"/> against the first creation rule that matches it.</summary>
    /// <exception cref="InvalidOperationException">The configuration or path is invalid, no rule matches, or the file's recipients cannot be read.</exception>
    public static async Task<int> Check(
        SecretsEnvironment environment,
        string root,
        string file,
        IReadOnlyList<string> proveWith,
        IReadOnlyList<string> proveAbsent,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var rules = await SecretsConfigurationFile.Read(root, cancellationToken).ConfigureAwait(false);
        var with = Identities(environment, proveWith);
        var absent = Identities(environment, proveAbsent);
        if (absent.Count > 0 && with.Count == 0)
        {
            throw new InvalidOperationException("--prove-absent-identity only means something next to a --prove-with-identity that decrypts the same file; give both.");
        }

        var timeout = environment.FileReadTimeout ?? GuardedFileReader.DefaultTimeout;
        foreach (var identity in with.Concat(absent))
        {
            var unusable = await IdentityFileCheck.Validate(identity.Path, timeout, cancellationToken).ConfigureAwait(false);
            if (unusable is not null)
            {
                throw new InvalidOperationException(unusable);
            }
        }

        var sops = with.Count + absent.Count > 0 ? ToolLocator.Sops(environment) : null;
        var (path, holders) = await Read(root, file, cancellationToken).ConfigureAwait(false);
        var rule = rules.FirstMatch(path) ?? throw new InvalidOperationException("No creation rule matches this file.");
        var verification = RecipientVerification.Compare(holders, rule);
        await Report(output, path.Value, verification, cancellationToken).ConfigureAwait(false);
        await EscapedOutput.Line(output, VerifyCommand.ProofNote, cancellationToken).ConfigureAwait(false);
        var exit = verification.IsExact ? ExitCodes.Success : ExitCodes.Differences;
        if (sops is null)
        {
            return exit;
        }

        var resolved = path.ResolveUnder(root);
        var absoluteFile = resolved.IsSuccess ? resolved.Value : throw new InvalidOperationException(resolved.ErrorDetails?.Detail ?? "The file path cannot be resolved under the root.");
        var holderDecrypted = false;
        foreach (var identity in with)
        {
            var trial = await Trial(sops, absoluteFile, identity, cancellationToken).ConfigureAwait(false);
            var can = trial.Outcome == SopsTrialOutcome.Decrypted;
            holderDecrypted |= can;
            exit = can ? exit : ExitCodes.Differences;
            await EscapedOutput.Line(output, $"{identity.Path}: {(can ? "can decrypt" : "cannot decrypt")}", cancellationToken).ConfigureAwait(false);
        }

        foreach (var identity in absent)
        {
            if (!holderDecrypted)
            {
                await EscapedOutput.Line(output, $"{identity.Path}: not tried, because no --prove-with-identity decrypted the file", cancellationToken).ConfigureAwait(false);
                continue;
            }

            var trial = await Trial(sops, absoluteFile, identity, cancellationToken).ConfigureAwait(false);
            var can = trial.Outcome == SopsTrialOutcome.Decrypted;
            exit = can ? ExitCodes.Differences : exit;
            await EscapedOutput.Line(output, $"{identity.Path}: {(can ? "CAN decrypt but must not" : "cannot decrypt, as required")}", cancellationToken).ConfigureAwait(false);
        }

        return exit;
    }

    private static List<AgeIdentityRef> Identities(SecretsEnvironment environment, IReadOnlyList<string> paths)
    {
        List<AgeIdentityRef> identities = [];
        foreach (var path in paths)
        {
            var identity = AgeIdentityRef.Create(path.Length > 0 ? Path.GetFullPath(path, environment.WorkingDirectory) : path, "identity");
            identities.Add(identity.IsSuccess ? identity.Value : throw new InvalidOperationException(identity.ErrorDetails?.Detail ?? "The identity file path is invalid."));
        }

        return identities;
    }

    /// <summary>
    /// A trial decryption; the plaintext is discarded unread. Only "this identity matches no recipient" and success are verdicts about the identity.
    /// Any other failure (a bad MAC, a parse error, a missing file, an unreadable identity, a timeout) says nothing about it and stops the run with exit 2.
    /// </summary>
    private static async Task<SopsTrial> Trial(ProcessSopsTool sops, string absoluteFile, AgeIdentityRef identity, CancellationToken cancellationToken)
    {
        var trial = await sops.TrialDecrypt(absoluteFile, identity, cancellationToken).ConfigureAwait(false);
        return trial.Outcome == SopsTrialOutcome.Failed
            ? throw new InvalidOperationException($"{identity.Path}: the file could not be decrypted (not an identity verdict): {trial.Detail}")
            : trial;
    }

    /// <summary>Writes the lines that say how a file differs from its rule, or a line that says its labels are consistent.</summary>
    public static async Task Report(TextWriter output, string path, RecipientVerification verification, CancellationToken cancellationToken)
    {
        if (verification.IsExact)
        {
            await EscapedOutput.Line(output, $"{path}: labels consistent", cancellationToken).ConfigureAwait(false);
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

        foreach (var concern in verification.Concerns)
        {
            await EscapedOutput.Line(output, $"{path}: weakened: {concern}", cancellationToken).ConfigureAwait(false);
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
