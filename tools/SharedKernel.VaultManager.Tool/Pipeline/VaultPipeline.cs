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
/// Reading and scanning are also available as separate stages.
/// </summary>
/// <param name="environment">Password prompts and clock.</param>
internal sealed class VaultPipeline(VaultCommandEnvironment environment)
{
    /// <summary>Prompt for the password that protects a new export.</summary>
    public const string NewExportPasswordPrompt = "Password for the new export";

    /// <summary>
    /// Reads every input file, computes each item's origin key as read, and completes SSH key items. A password-protected
    /// input asks for its password through <see cref="VaultCommandEnvironment.ReadPassword"/>.
    /// </summary>
    /// <param name="inputPaths">Vault export files, in order.</param>
    /// <returns>Items in input order, with their input paths, and the read warnings.</returns>
    /// <exception cref="ArgumentException">No input file was given.</exception>
    /// <exception cref="InvalidDataException">An input is too large, unsupported, malformed, account-restricted, or its password is wrong.</exception>
    /// <exception cref="IOException">An input file cannot be read.</exception>
    public LoadedVault Load(IReadOnlyList<string> inputPaths)
    {
        ArgumentNullException.ThrowIfNull(inputPaths);
        if (inputPaths.Count == 0)
        {
            throw new ArgumentException("At least one input file is needed.", nameof(inputPaths));
        }

        var read = new List<(VaultItem Item, string InputPath)>();
        var warnings = new List<string>();
        foreach (string path in inputPaths)
        {
            VaultReadResult result = VaultFileReader.Read(path, fileName => environment.ReadPassword("Password for " + fileName));
            read.AddRange(result.Export.Items.Select(item => (item, path)));
            string inputName = Path.GetFileName(path);
            warnings.AddRange(result.Warnings.Select(warning => inputName + ": " + warning));
        }

        IReadOnlyList<string> keys = OriginKeys.Compute([.. read.Select(entry => entry.Item)]);
        SourceItem[] items = [.. read.Select((entry, index) => new SourceItem(SshKeyNormalizer.Normalize(entry.Item), keys[index], entry.InputPath))];
        return new LoadedVault(items, warnings);
    }

    /// <summary>
    /// Scans every item for secrets, and the text attachments stored in input archives. Attachments are opened through
    /// <see cref="SafeArchive"/>, and binary attachments are never decoded.
    /// </summary>
    /// <param name="vault">Loaded items.</param>
    /// <returns>Items with their findings and attachment texts, and every warning so far.</returns>
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
            foreach (AttachmentRef attachment in source.Item.Attachments)
            {
                if (attachment.SourcePath is not { } entryPath || ReadText(source, attachment, entryPath, archives, warnings) is not { } text)
                {
                    continue;
                }

                string fieldPath = UniqueAttachmentPath(attachment.FileName, texts);
                texts.Add(fieldPath, text);
                findings.AddRange(SecretScanner.Default.ScanText(text, fieldPath, attachment.FileName, source.Item));
            }

            items.Add(new ScannedItem(source, findings, texts));
        }

        return new ScannedVault(items, warnings);
    }

    /// <summary>
    /// Runs every stage. The merged vault is written only when an output is requested, no field needs a decision, and the
    /// writer reports no error; the password of an encrypted output is then read through
    /// <see cref="VaultCommandEnvironment.ReadNewPassword"/>.
    /// </summary>
    /// <param name="request">Inputs, decisions, and output.</param>
    /// <returns>The merged vault, reports, problems, and warnings.</returns>
    /// <exception cref="IOException">
    /// The output file exists and may not be replaced, or it is one of the inputs. Both are checked before any input is
    /// read.
    /// </exception>
    /// <exception cref="InvalidDataException">An input is too large, unsupported, malformed, account-restricted, or its password is wrong.</exception>
    public PipelineResult Run(PipelineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Output is { } requested)
        {
            CheckOutput(requested, request.Inputs);
        }

        ScannedVault scanned = Scan(Load(request.Inputs));
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

    private static string? ReadText(SourceItem source, AttachmentRef attachment, string entryPath, InputArchives archives, List<string> warnings)
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

        ExtractedText? extracted;
        try
        {
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

        return extracted?.Text;
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

    private static PlacedVault Place(ScannedVault scanned, MergeDecisions decisions)
    {
        var items = new List<VaultItem>(scanned.Items.Count);
        var keys = new List<string>(scanned.Items.Count);
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
                (VaultItem created, string key) = Adopt(placement.NewItems[index], source, placement.NewItemFindingIds[index]);
                items.Add(created);
                keys.Add(key);
            }

            problems.AddRange(placement.Problems.Select(problem => new PlacementProblem(source.Item.Name, source.OriginKey, problem)));
        }

        return new PlacedVault(items, keys, findings, problems);
    }

    // An item created by placement is named after its source item and the finding that created it, so it has the same
    // origin key on every run and matches and merges like an item read from a file.
    private static (VaultItem Item, string OriginKey) Adopt(VaultItem created, SourceItem source, string findingId)
    {
        ItemOrigin origin = source.Item.Origin;
        string prefix = origin.Provider + "|" + origin.SourceFile + "|";
        string sourceId = source.OriginKey.StartsWith(prefix, StringComparison.Ordinal) ? source.OriginKey[prefix.Length..] : source.OriginKey;
        string originalId = sourceId + "/" + findingId;
        VaultItem adopted = created with
        {
            Origin = origin with { OriginalId = originalId },
            CreationDate = created.CreationDate ?? source.Item.CreationDate,
            RevisionDate = created.RevisionDate ?? source.Item.RevisionDate,
        };
        return (adopted, prefix + originalId);
    }

    private static List<ManualAttachment> ManualAttachments(MatchResult match, VaultExport merged)
    {
        var attachments = new List<ManualAttachment>();
        for (int index = 0; index < match.Groups.Count; index++)
        {
            string itemName = merged.Items[index].Name;
            var files = match.Groups[index].Members.SelectMany(member => member.Attachments.Select(attachment => (Member: member, Attachment: attachment)));

            // The merged item lists a file once per name and size; point to a copy included in an export when there is one.
            foreach (var sameFile in files.GroupBy(file => (file.Attachment.FileName, file.Attachment.Size)))
            {
                var (member, attachment) = sameFile.OrderBy(file => file.Attachment.SourcePath is null).First();
                attachments.Add(new ManualAttachment(itemName, attachment.FileName, member.Origin.SourceFile, attachment.SourcePath));
            }
        }

        return attachments;
    }

    private static void CheckOutput(PipelineOutput output, IReadOnlyList<string> inputs)
    {
        ArgumentException.ThrowIfNullOrEmpty(output.Path);

        string target = Path.GetFullPath(output.Path);
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (inputs.Any(input => string.Equals(Path.GetFullPath(input), target, comparison)))
        {
            throw new IOException($"Output file '{target}' is also an input file. Choose another output path.");
        }

        if (!output.Force && File.Exists(target))
        {
            throw new IOException($"Output file '{target}' already exists. Choose another path or allow overwriting.");
        }
    }

    private void WriteOutput(string json, PipelineOutput output)
    {
        string contents = output.Plaintext
            ? json
            : PasswordProtectedExport.Encrypt(json, environment.ReadNewPassword(NewExportPasswordPrompt), KdfConfig.DefaultPbkdf2);
        SecureFile.WriteAllText(output.Path, contents, output.Force);
    }

    private sealed record PlacedVault(
        IReadOnlyList<VaultItem> Items,
        IReadOnlyList<string> OriginKeys,
        IReadOnlyList<ItemFindingsResult> Findings,
        IReadOnlyList<PlacementProblem> Problems);
}
