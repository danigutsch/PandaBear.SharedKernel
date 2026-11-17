using System.Text;

namespace PandaBear.Secrets;

/// <summary>
/// Produces a new version of an encrypted file with sops: new recipients, a new data key, or an edited document. Each
/// operation reads the file and its rule, refuses a weakened file, checks that the new version carries exactly the rule's
/// recipients with nothing weakened, and replaces the file only when it is unchanged since it was read.
/// </summary>
internal static class RewriteOperation
{
    private const int MaxFileBytes = 16 * 1024 * 1024;

    /// <summary>Makes the file's recipients exactly its rule's, keeping the data key.</summary>
    /// <exception cref="InvalidOperationException">The request is invalid or refused, or sops failed.</exception>
    public static async Task<int> UpdateRecipients(string root, SecretsPrograms programs, UpdateRecipientsSettings settings, TextWriter output, CancellationToken cancellationToken)
    {
        var target = await Target.Read(root, programs, settings.File, cancellationToken).ConfigureAwait(false);
        target.RefuseWeakened();
        if (target.Verification.IsExact)
        {
            await EscapedOutput.Line(output, $"{target.Path}: labels already consistent with the rule; nothing to update.", cancellationToken).ConfigureAwait(false);
            return SecretsExitCodes.Success;
        }

        var sops = programs.Sops();
        await ReportChanges(output, target, cancellationToken).ConfigureAwait(false);
        if (!settings.Force)
        {
            await EscapedOutput.Line(output, "Preview: nothing was written. Run again with --force to update the recipients.", cancellationToken).ConfigureAwait(false);
            return SecretsExitCodes.Success;
        }

        var identity = Identity(settings.IdentityPath);
        var updated = await sops.UpdateKeys(target.AbsolutePath, identity, target.Rule.Recipients, cancellationToken).ConfigureAwait(false);
        await target.Replace(updated.IsSuccess ? updated.Value : throw Failed(updated.ErrorDetails?.Detail), cancellationToken).ConfigureAwait(false);
        await EscapedOutput.Line(output, $"{target.Path}: recipients updated; the data key is unchanged.", cancellationToken).ConfigureAwait(false);
        if (target.Verification.Unexpected.Count > 0 || target.Verification.OtherKeyTypes.Count > 0)
        {
            await EscapedOutput.Line(output, $"A removed key holder may already know the data key from an earlier version; run 'rotate {target.Path}' to replace it.", cancellationToken).ConfigureAwait(false);
        }

        return SecretsExitCodes.Success;
    }

    /// <summary>Encrypts the file with a new data key for exactly its rule's recipients.</summary>
    /// <exception cref="InvalidOperationException">The request is invalid or refused, or sops failed.</exception>
    public static async Task<int> Rotate(string root, SecretsPrograms programs, RotateSettings settings, TextWriter output, CancellationToken cancellationToken)
    {
        var target = await Target.Read(root, programs, settings.File, cancellationToken).ConfigureAwait(false);
        target.RefuseWeakened();
        if (target.Verification.OtherKeyTypes.Count > 0)
        {
            throw new InvalidOperationException($"{target.Path}: a non-age key holder ({string.Join(", ", target.Verification.OtherKeyTypes)}) holds a key, which rotate cannot remove; run 'recipients update {target.Path}' first.");
        }

        var sops = programs.Sops();
        await ReportChanges(output, target, cancellationToken).ConfigureAwait(false);
        await EscapedOutput.Line(output, $"{target.Path}: a new data key for the {target.Rule.Recipients.Count} recipients of its rule; every value is encrypted again.", cancellationToken).ConfigureAwait(false);
        if (!settings.Force)
        {
            await EscapedOutput.Line(output, "Preview: nothing was written. Run again with --force to rotate.", cancellationToken).ConfigureAwait(false);
            return SecretsExitCodes.Success;
        }

        var identity = Identity(settings.IdentityPath);
        var rotated = await sops.Rotate(target.AbsolutePath, identity, target.Rule.Recipients, cancellationToken).ConfigureAwait(false);
        await target.Replace(rotated.IsSuccess ? rotated.Value : throw Failed(rotated.ErrorDetails?.Detail), cancellationToken).ConfigureAwait(false);
        await EscapedOutput.Line(output, $"{target.Path}: rotated.", cancellationToken).ConfigureAwait(false);
        return SecretsExitCodes.Success;
    }

    /// <summary>Lets a person edit the decrypted document in a private memory-backed directory.</summary>
    /// <exception cref="InvalidOperationException">The request is invalid or refused, or sops failed.</exception>
    public static async Task<int> Edit(string root, SecretsPrograms programs, EditSettings settings, TextWriter output, CancellationToken cancellationToken)
    {
        var target = await Target.Read(root, programs, settings.File, cancellationToken).ConfigureAwait(false);
        target.RefuseWeakened();
        if (!target.Verification.IsExact)
        {
            throw new InvalidOperationException($"{target.Path}: its recipients are not exactly its rule's; run 'recipients update {target.Path}' first.");
        }

        var identity = Identity(settings.IdentityPath);
        var editor = EditorCommand.Create(settings.Editor, programs.ExecutableResolver);
        if (editor.IsFailure)
        {
            throw new InvalidOperationException(editor.ErrorDetails?.Detail ?? "The editor command is invalid.");
        }

        var sops = programs.Sops();
        var directory = MemoryBackedDirectory.Create(settings.TemporaryRoot);
        string document;
        try
        {
            var session = new SopsEditSession
            {
                Editor = editor.Value,
                PrivateDirectory = directory,
                TerminalEnvironment = TerminalEnvironment(settings.TerminalEnvironment),
            };
            var edited = await sops.Edit(target.AbsolutePath, identity, session, cancellationToken).ConfigureAwait(false);
            document = edited.IsSuccess ? edited.Value : throw Failed(edited.ErrorDetails?.Detail);
        }
        finally
        {
            MemoryBackedDirectory.Remove(directory);
        }

        if (Encoding.UTF8.GetBytes(document).AsSpan().SequenceEqual(target.Bytes))
        {
            await EscapedOutput.Line(output, $"{target.Path}: unchanged.", cancellationToken).ConfigureAwait(false);
            return SecretsExitCodes.Success;
        }

        await target.Replace(document, cancellationToken).ConfigureAwait(false);
        await EscapedOutput.Line(output, $"{target.Path}: saved.", cancellationToken).ConfigureAwait(false);
        return SecretsExitCodes.Success;
    }

    private static AgeIdentityRef Identity(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new InvalidOperationException("An identity file that can decrypt the data key is required.");
        }

        var identity = AgeIdentityRef.Create(path);
        return identity.IsSuccess ? identity.Value : throw new InvalidOperationException(identity.ErrorDetails?.Detail ?? "The identity file path is invalid.");
    }

    private static InvalidOperationException Failed(string? detail) => new($"{detail ?? "sops failed."} Nothing was written.");

    private static Dictionary<string, string> TerminalEnvironment(IReadOnlyDictionary<string, string> host)
    {
        Dictionary<string, string> kept = new(StringComparer.Ordinal);
        foreach (var name in EditSettings.TerminalVariableNames)
        {
            if (host.TryGetValue(name, out var value) && value.Length > 0)
            {
                kept[name] = name == "PATH"
                    ? string.Join(Path.PathSeparator, value.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Where(Path.IsPathFullyQualified))
                    : value;
            }
        }

        return kept;
    }

    private static async Task ReportChanges(TextWriter output, Target target, CancellationToken cancellationToken)
    {
        foreach (var missing in target.Verification.Missing)
        {
            await EscapedOutput.Line(output, $"{target.Path}: add recipient {missing}", cancellationToken).ConfigureAwait(false);
        }

        foreach (var unexpected in target.Verification.Unexpected)
        {
            await EscapedOutput.Line(output, $"{target.Path}: remove recipient {unexpected}", cancellationToken).ConfigureAwait(false);
        }

        foreach (var other in target.Verification.OtherKeyTypes)
        {
            await EscapedOutput.Line(output, $"{target.Path}: remove non-age key holder {other}", cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>A file, its rule, the bytes it was read with, and how its labels compare with the rule.</summary>
    private sealed class Target
    {
        private Target(EncryptedFilePath path, string absolutePath, SopsFormat format, SopsCreationRule rule, byte[] bytes, RecipientVerification verification, TimeSpan readTimeout)
        {
            ReadTimeout = readTimeout;
            Path = path;
            AbsolutePath = absolutePath;
            Format = format;
            Rule = rule;
            Bytes = bytes;
            Verification = verification;
        }

        public EncryptedFilePath Path
        {
            get;
        }

        public string AbsolutePath
        {
            get;
        }

        public SopsFormat Format
        {
            get;
        }

        public SopsCreationRule Rule
        {
            get;
        }

        public byte[] Bytes
        {
            get;
        }

        public RecipientVerification Verification
        {
            get;
        }

        public TimeSpan ReadTimeout
        {
            get;
        }

        public static async Task<Target> Read(string root, SecretsPrograms programs, string file, CancellationToken cancellationToken)
        {
            var rules = await SecretsConfigurationFile.Read(root, cancellationToken).ConfigureAwait(false);
            var created = EncryptedFilePath.Create(file);
            var path = created.IsSuccess ? created.Value : throw new InvalidOperationException(created.ErrorDetails?.Detail ?? "The file path is invalid.");
            var rule = rules.FirstMatch(path) ?? throw new InvalidOperationException("No creation rule matches this file.");
            var format = path.Format ?? throw new InvalidOperationException("Only YAML and JSON SOPS files can be rewritten.");
            var resolved = path.ResolveUnder(root);
            var absolute = resolved.IsSuccess ? resolved.Value : throw new InvalidOperationException(resolved.ErrorDetails?.Detail ?? "The file path cannot be resolved under the root.");
            if (!File.Exists(absolute))
            {
                throw new InvalidOperationException($"{path}: the file does not exist.");
            }

            var (bytes, problem) = await GuardedFileReader.Read(absolute, MaxFileBytes, programs.FileReadTimeout, cancellationToken).ConfigureAwait(false);
            if (problem is not null)
            {
                throw new InvalidOperationException($"{path}: {problem}");
            }

            var holders = SopsMetadataReader.Read(Encoding.UTF8.GetString(bytes), format);
            return holders.IsSuccess
                ? new Target(path, absolute, format, rule, bytes, RecipientVerification.Compare(holders.Value, rule), programs.FileReadTimeout)
                : throw new InvalidOperationException($"{path}: {holders.ErrorDetails?.Detail}");
        }

        public void RefuseWeakened()
        {
            if (Verification.Concerns.Count > 0)
            {
                throw new InvalidOperationException($"{Path}: weakened ({string.Join("; ", Verification.Concerns)}); sops keeps that in every new version. Decrypt it and encrypt it again.");
            }
        }

        /// <summary>Checks that <paramref name="document"/> carries exactly the rule's recipients with nothing weakened, then replaces the file.</summary>
        public async Task Replace(string document, CancellationToken cancellationToken)
        {
            var holders = SopsMetadataReader.Read(document, Format);
            if (holders.IsFailure || !RecipientVerification.Compare(holders.Value, Rule).IsExact)
            {
                throw new InvalidOperationException($"{Path}: the new version does not carry exactly the rule's recipients with nothing weakened; nothing was written.");
            }

            await EncryptedFileReplacement.Replace(AbsolutePath, Bytes, document, ReadTimeout, cancellationToken).ConfigureAwait(false);
        }
    }
}
