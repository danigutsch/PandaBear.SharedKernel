using System.Globalization;
using PandaBear.VaultManager.Tool.Analysis;
using PandaBear.VaultManager.Tool.Attachments;
using PandaBear.VaultManager.Tool.Crypto;
using PandaBear.VaultManager.Tool.Formats;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.IO;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Pipeline;

/// <summary>
/// Runs the vault pipeline: read, normalize SSH keys, scan, place accepted findings, match, merge, analyze, and write.
/// Reading and scanning are also available as separate stages, and <see cref="Merge"/> runs the remaining stages on an
/// already read and scanned vault, for example each time the user changes a decision in the review UI.
/// </summary>
/// <remarks>
/// The errors a caller reports to the user are <see cref="IOException"/>, <see cref="InvalidDataException"/>, and
/// <see cref="InvalidOperationException"/>. Their messages name files and items, never vault values; an inner exception
/// from a parser can quote input text, so report <see cref="Exception.Message"/> rather than the exception chain.
/// <see cref="ArgumentException"/> means the caller passed no input or an empty output path.
/// </remarks>
/// <param name="environment">Password prompts and clock.</param>
internal sealed class VaultPipeline(VaultCommandEnvironment environment)
{
    /// <summary>Prompt for the password that protects a new export.</summary>
    public const string NewExportPasswordPrompt = "Password for the new export";

    /// <summary>
    /// Most findings kept for one attachment. Later findings, by position, are dropped, and a warning says that only
    /// part of the attachment is covered, so a large log or list cannot flood the report and the decisions.
    /// </summary>
    public const int MaxAttachmentFindings = 1000;

    /// <summary>
    /// Reads every input file, computes each item's origin key as read, and completes SSH key items. A password-protected
    /// input asks for its password through <see cref="VaultCommandEnvironment.ReadPassword"/>.
    /// </summary>
    /// <param name="inputPaths">Vault export files, in order.</param>
    /// <returns>The input paths, the items in input order with their input paths, and the read warnings.</returns>
    /// <exception cref="ArgumentException">No input file was given.</exception>
    /// <exception cref="IOException">
    /// An input file does not exist, is a directory, or cannot be read, also when access to it is denied.
    /// </exception>
    /// <exception cref="InvalidDataException">An input is too large, unsupported, malformed, account-restricted, or its password is wrong.</exception>
    /// <exception cref="InvalidOperationException">
    /// The password prompt of a password-protected input failed, for example because standard input is not a terminal.
    /// </exception>
    public LoadedVault Load(IReadOnlyList<string> inputPaths)
    {
        ArgumentNullException.ThrowIfNull(inputPaths);
        if (inputPaths.Count == 0)
        {
            throw new ArgumentException("At least one input file is needed.", nameof(inputPaths));
        }

        string[] paths = [.. inputPaths];
        var read = new List<(VaultItem Item, string InputPath)>();
        var warnings = new List<string>();
        foreach (string path in paths)
        {
            VaultReadResult result = VaultFileReader.Read(path, fileName => environment.ReadPassword("Password for " + fileName));
            read.AddRange(result.Export.Items.Select(item => (item, path)));
            string inputName = Path.GetFileName(path);
            warnings.AddRange(result.Warnings.Select(warning => inputName + ": " + warning));
        }

        IReadOnlyList<string> keys = OriginKeys.Compute([.. read.Select(entry => entry.Item)]);
        SourceItem[] items = [.. read.Select((entry, index) => new SourceItem(SshKeyNormalizer.Normalize(entry.Item), keys[index], entry.InputPath))];
        return new LoadedVault(paths, items, warnings);
    }

    /// <summary>
    /// Scans every item for secrets, and the text attachments stored in input archives. Attachments are opened through
    /// <see cref="SafeArchive"/>; every attachment read from an archive gets its <see cref="AttachmentRef.ContentHash"/>,
    /// and binary attachments are hashed but never decoded. An attachment keeps at most
    /// <see cref="MaxAttachmentFindings"/> findings.
    /// </summary>
    /// <param name="vault">Loaded items.</param>
    /// <returns>The input paths, the items with their findings and attachment texts, and every warning so far.</returns>
    public static ScannedVault Scan(LoadedVault vault)
    {
        ArgumentNullException.ThrowIfNull(vault);

        var warnings = new List<string>(vault.Warnings);
        var items = new List<ScannedItem>(vault.Items.Count);
        using var archives = new InputArchives();
        foreach (SourceItem source in vault.Items)
        {
            var findings = new List<SecretFinding>(SecretScanner.Default.Scan(source.Item));
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            var attachments = new List<AttachmentRef>(source.Item.Attachments.Count);
            foreach (AttachmentRef attachment in source.Item.Attachments)
            {
                if (attachment.SourcePath is not { } entryPath || ReadAttachment(source, attachment, entryPath, archives, warnings) is not { } content)
                {
                    attachments.Add(attachment);
                    continue;
                }

                attachments.Add(attachment with { ContentHash = content.Hash });
                if (content.Text is not { } text)
                {
                    continue;
                }

                string fieldPath = UniqueAttachmentPath(attachment.FileName, texts);
                texts.Add(fieldPath, text);
                IReadOnlyList<SecretFinding> attachmentFindings = SecretScanner.Default.ScanText(text, fieldPath, attachment.FileName, source.Item);
                findings.AddRange(LimitFindings(attachmentFindings, source, attachment, warnings));
            }

            SourceItem scannedSource = attachments.SequenceEqual(source.Item.Attachments)
                ? source
                : source with { Item = source.Item with { Attachments = attachments } };
            items.Add(new ScannedItem(scannedSource, findings, texts));
        }

        return new ScannedVault(vault.InputPaths, items, warnings);
    }

    /// <summary>
    /// Runs every stage: checks the output, reads and scans the inputs, and continues with <see cref="Merge"/>.
    /// </summary>
    /// <param name="request">Inputs, decisions, policy, and output.</param>
    /// <returns>The merged vault, reports, problems, and warnings.</returns>
    /// <exception cref="ArgumentException">No input file was given, or the output path is empty.</exception>
    /// <exception cref="IOException">
    /// The output cannot be used: it names an input, also through a symbolic link or in different case; its directory does
    /// not exist; it is a directory; or it exists and may not be replaced. These are checked before any input is read.
    /// Also thrown when an input is missing, is a directory, or cannot be read, or the output cannot be written, including
    /// when access is denied.
    /// </exception>
    /// <exception cref="InvalidDataException">An input is too large, unsupported, malformed, account-restricted, or its password is wrong.</exception>
    /// <exception cref="InvalidOperationException">
    /// A password prompt failed, for example because standard input is not a terminal, or the password for an encrypted
    /// output is shorter than <see cref="PasswordReader.MinimumNewPasswordLength"/>. Nothing is written then.
    /// </exception>
    public PipelineResult Run(PipelineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Output is { } output)
        {
            CheckOutput(output, request.Inputs);
        }

        return Merge(Scan(Load(request.Inputs)), request);
    }

    /// <summary>
    /// Places the accepted findings of a scanned vault, then matches, merges, and analyzes it. The merged vault is written
    /// only when an output is requested, no field needs a decision, and the writer reports no error; the password of an
    /// encrypted output is then read through <see cref="VaultCommandEnvironment.ReadNewPassword"/>. Inputs are not read
    /// again and their passwords are not asked for, so the review UI can merge again after each change of decisions.
    /// </summary>
    /// <param name="scanned">Vault from <see cref="Scan"/>; it is not changed.</param>
    /// <param name="request">Decisions, policy, and output.</param>
    /// <returns>The merged vault, reports, problems, and warnings.</returns>
    /// <exception cref="ArgumentException">The output path is empty.</exception>
    /// <exception cref="IOException">
    /// The output cannot be used, as described for <see cref="Run"/>, checked against the inputs of
    /// <paramref name="scanned"/> before anything else; or the output cannot be written, including when access is denied.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The new-password prompt failed, or the password for an encrypted output is shorter than
    /// <see cref="PasswordReader.MinimumNewPasswordLength"/>. Nothing is written then.
    /// </exception>
    public PipelineResult Merge(ScannedVault scanned, MergeRequest request)
    {
        ArgumentNullException.ThrowIfNull(scanned);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Output is { } requested)
        {
            CheckOutput(requested, scanned.InputPaths);
        }

        PlacedVault placed = Place(scanned, request.Decisions);
        MatchResult match = DuplicateMatcher.Match(placed.Items, request.Decisions.Overrides, placed.OriginKeys);
        MergeResult merge = new Merger(environment.Time).Merge(match.Groups, request.Policy, request.Decisions);
        PasswordHealthReport health = PasswordHealthReport.Analyze(merge.Merged);
        BitwardenWriteResult written = BitwardenJsonWriter.Write(merge.Merged);
        bool outputWritten = false;
        if (request.Output is { } output && merge.Unresolved.Count == 0 && written.Errors.Count == 0)
        {
            WriteOutput(written.Json, output);
            outputWritten = true;
        }

        return new PipelineResult
        {
            Merged = merge.Merged,
            Match = match,
            Unresolved = merge.Unresolved,
            MergeReport = merge.Report,
            Findings = placed.Findings,
            PlacementProblems = placed.Problems,
            PasswordHealth = health,
            ManualAttachments = ManualAttachments(match, merge.Merged),
            WriteErrors = written.Errors,
            WriteWarnings = written.Warnings,
            ReadWarnings = scanned.Warnings,
            Output = request.Output,
            OutputWritten = outputWritten,
        };
    }

    // Reads an attachment from its input archive: the SHA-256 of its bytes, and its text when it is a text kind. Returns
    // null, after a warning, when the archive or the entry cannot be read.
    private static (string Hash, string? Text)? ReadAttachment(SourceItem source, AttachmentRef attachment, string entryPath, InputArchives archives, List<string> warnings)
    {
        if (archives.Open(source.InputPath, warnings) is not { } archive)
        {
            return null;
        }

        if (archive.Find(entryPath) is not { } entry)
        {
            warnings.Add($"\"{source.Item.Name}\": attachment \"{attachment.FileName}\" was not scanned because it is no longer in {Path.GetFileName(source.InputPath)}.");
            return null;
        }

        string hash;
        ExtractedText? extracted;
        try
        {
            hash = Convert.ToHexStringLower(archive.ComputeSha256(entry));
            extracted = archive.ReadText(entry);
        }
        catch (InvalidDataException exception)
        {
            warnings.Add($"\"{source.Item.Name}\": attachment \"{attachment.FileName}\" was not scanned ({exception.Message})");
            return null;
        }
        catch (IOException exception)
        {
            warnings.Add($"\"{source.Item.Name}\": attachment \"{attachment.FileName}\" was not scanned ({exception.Message})");
            return null;
        }

        if (extracted is { Truncated: true } or { TruncatedLines: > 0 })
        {
            warnings.Add($"\"{source.Item.Name}\": attachment \"{attachment.FileName}\" was scanned only in part because it exceeds the text scanning limits.");
        }

        return (hash, extracted?.Text);
    }

    // Two attachments of one item can share a file name; their findings still need distinct field paths.
    private static string UniqueAttachmentPath(string fileName, Dictionary<string, string> texts)
    {
        string path = SecretLocation.Attachment(fileName);
        for (int number = 2; texts.ContainsKey(path); number++)
        {
            path = SecretLocation.Attachment(string.Create(CultureInfo.InvariantCulture, $"{fileName} ({number})"));
        }

        return path;
    }

    private static IReadOnlyList<SecretFinding> LimitFindings(IReadOnlyList<SecretFinding> findings, SourceItem source, AttachmentRef attachment, List<string> warnings)
    {
        if (findings.Count <= MaxAttachmentFindings)
        {
            return findings;
        }

        warnings.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"\"{source.Item.Name}\": attachment \"{attachment.FileName}\" was scanned only in part because it has more than {MaxAttachmentFindings} findings; only the first {MaxAttachmentFindings} are listed."));
        return [.. findings.OrderBy(finding => finding.Location.Start).Take(MaxAttachmentFindings)];
    }

    private static PlacedVault Place(ScannedVault scanned, MergeDecisions decisions)
    {
        var items = new List<VaultItem>(scanned.Items.Count);
        var keys = new List<string>(scanned.Items.Count);
        var takenKeys = new HashSet<string>(scanned.Items.Select(item => item.Source.OriginKey), StringComparer.Ordinal);
        var findings = new List<ItemFindingsResult>();
        var problems = new List<PlacementProblem>();
        foreach (ScannedItem scannedItem in scanned.Items)
        {
            SourceItem source = scannedItem.Source;
            SecretFinding[] accepted = [.. scannedItem.Findings.Where(finding => decisions.Applies(source.OriginKey, finding))];
            if (scannedItem.Findings.Count > 0)
            {
                findings.Add(new ItemFindingsResult(source.OriginKey, source.Item.Name, scannedItem.Findings, [.. accepted.Select(finding => finding.Id)]));
            }

            PlacementResult placement = SecretPlacer.Apply(source.Item, accepted, scannedItem.AttachmentTexts);
            items.Add(placement.Item);
            keys.Add(source.OriginKey);
            for (int index = 0; index < placement.NewItems.Count; index++)
            {
                (VaultItem created, string key) = Adopt(placement.NewItems[index], source, placement.NewItemFindingIds[index], takenKeys);
                items.Add(created);
                keys.Add(key);
            }

            problems.AddRange(placement.Problems.Select(problem => new PlacementProblem(source.Item.Name, source.OriginKey, problem)));
        }

        return new PlacedVault(items, keys, findings, problems);
    }

    // An item created by placement is named after its source item and the finding that created it, so it has the same
    // origin key on every run and matches and merges like an item read from a file. A suffix keeps the key unique when an
    // input item already has it.
    private static (VaultItem Item, string OriginKey) Adopt(VaultItem created, SourceItem source, string findingId, HashSet<string> takenKeys)
    {
        ItemOrigin origin = source.Item.Origin;
        string prefix = origin.Provider + "|" + origin.SourceFile + "|";
        string sourceId = source.OriginKey.StartsWith(prefix, StringComparison.Ordinal) ? source.OriginKey[prefix.Length..] : source.OriginKey;
        string key = OriginKeys.MakeUnique(prefix + sourceId + "/" + findingId, takenKeys);
        VaultItem adopted = created with
        {
            Origin = origin with { OriginalId = key[prefix.Length..] },
            CreationDate = created.CreationDate ?? source.Item.CreationDate,
            RevisionDate = created.RevisionDate ?? source.Item.RevisionDate,
        };
        return (adopted, key);
    }

    private static List<ManualAttachment> ManualAttachments(MatchResult match, VaultExport merged)
    {
        var attachments = new List<ManualAttachment>();
        for (int index = 0; index < match.Groups.Count; index++)
        {
            string itemName = merged.Items[index].Name;
            foreach ((VaultItem member, AttachmentRef attachment) in DistinctFiles(match.Groups[index].Members))
            {
                attachments.Add(new ManualAttachment(itemName, attachment.FileName, member.Origin.SourceFile, attachment.SourcePath));
            }
        }

        return attachments;
    }

    // Each attachment of a member is a file of its own, even when two share a name. An attachment of another member is a
    // copy of a listed file only when both were read from their archives and have the same name and content hash; the
    // n-th such attachment of a member is a copy of the n-th listed file. A file whose content is unknown is listed for
    // every member that has it. Members are visited newest first, as GroupMerger orders them, so a file found on several
    // members points to the newest member's copy, the one the merged item keeps.
    private static List<(VaultItem Member, AttachmentRef Attachment)> DistinctFiles(IReadOnlyList<VaultItem> members)
    {
        var files = new List<(VaultItem Member, AttachmentRef Attachment)>();
        var listed = new Dictionary<(string FileName, string ContentHash), int>();
        foreach (VaultItem member in members.OrderByDescending(member => member.LastModified.HasValue).ThenByDescending(member => member.LastModified))
        {
            var occurrences = new Dictionary<(string FileName, string ContentHash), int>();
            foreach (AttachmentRef attachment in member.Attachments)
            {
                if (attachment.ContentHash is not { } contentHash)
                {
                    files.Add((member, attachment));
                    continue;
                }

                var file = (attachment.FileName, contentHash);
                int occurrence = occurrences.GetValueOrDefault(file) + 1;
                occurrences[file] = occurrence;
                if (occurrence > listed.GetValueOrDefault(file))
                {
                    listed[file] = occurrence;
                    files.Add((member, attachment));
                }
            }
        }

        return files;
    }

    // Checked before any input is read and again before merging, so a bad output fails before the password prompts.
    private static void CheckOutput(PipelineOutput output, IReadOnlyList<string> inputs)
    {
        ArgumentException.ThrowIfNullOrEmpty(output.Path);

        string target = Path.GetFullPath(output.Path);
        if (inputs.Any(input => FilePaths.MayNameSameFile(input, target)))
        {
            throw new IOException($"Output file '{target}' is also an input file. Choose another output path.");
        }

        if (Directory.Exists(target))
        {
            throw new IOException($"Output path '{target}' is a directory. Choose a file path.");
        }

        string? directory = Path.GetDirectoryName(target);
        if (directory is null || !Directory.Exists(directory))
        {
            throw new IOException($"Output directory '{directory ?? target}' does not exist. Create it or choose another output path.");
        }

        if (!output.Force && File.Exists(target))
        {
            throw new IOException($"Output file '{target}' already exists. Choose another path or allow overwriting.");
        }
    }

    private void WriteOutput(string json, PipelineOutput output)
    {
        string contents = output.Plaintext ? json : Encrypt(json);
        SecureFile.WriteAllText(output.Path, contents, output.Force);
    }

    private string Encrypt(string json)
    {
        string password = environment.ReadNewPassword(NewExportPasswordPrompt);
        if (password.Length < PasswordReader.MinimumNewPasswordLength)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"The password for the new export needs at least {PasswordReader.MinimumNewPasswordLength} characters because it protects every secret in the file. Nothing was written."));
        }

        return PasswordProtectedExport.Encrypt(json, password, KdfConfig.DefaultPbkdf2);
    }

    private sealed record PlacedVault(
        IReadOnlyList<VaultItem> Items,
        IReadOnlyList<string> OriginKeys,
        IReadOnlyList<ItemFindingsResult> Findings,
        IReadOnlyList<PlacementProblem> Problems);
}
