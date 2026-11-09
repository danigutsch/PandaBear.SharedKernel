using System.Globalization;
using System.Text;
using PandaBear.Results;

namespace PandaBear.Secrets.Tool;

/// <summary>
/// Implements <c>verify</c>: every SOPS-encrypted file in the repository, whatever its name or directory, is judged against the first creation rule
/// whose path expression matches it. A file passes when its labels are consistent with the rule, which proves labels and structure and not who can
/// decrypt. It starts no process unless <c>--base</c> asks for git. Verifying nothing, a path or entry that cannot be judged, and a file that no
/// rule covers are not success.
/// </summary>
internal static class VerifyCommand
{
    /// <summary>The note printed once per run, so a pass is not read as more than it proves.</summary>
    public const string ProofNote = "Note: this proves labels and structure, not who can decrypt. Compare with a base revision (--base) or trial-decrypt (recipients check --prove-with-identity) to go further.";

    private const string SopsConfigurationName = ".sops.yaml";
    private const long MaxFileBytes = 16 * 1024 * 1024;

    /// <summary>
    /// Returns 0 when every file is covered, consistent, and (with <c>--base</c>) unchanged, 1 when one differs, is weakened, is uncovered, or has a changed
    /// recipient, and 2 when nothing was verified or an entry cannot be read or judged. A failure outranks a difference.
    /// </summary>
    /// <exception cref="InvalidOperationException">The configuration is invalid or git cannot be used.</exception>
    public static async Task<int> Run(
        SecretsEnvironment environment,
        string root,
        VerifyOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var rules = await SecretsConfigurationFile.Read(root, cancellationToken).ConfigureAwait(false);
        var baseline = options.BaseRevision is null ? null : await GitBase.Open(environment, root, options.BaseRevision, cancellationToken).ConfigureAwait(false);
        Tally tally = new();
        foreach (var entry in SopsFileFinder.Find(root))
        {
            await Examine(entry, root, rules, baseline, tally, output, error, cancellationToken).ConfigureAwait(false);
        }

        await EscapedOutput.Line(
            output,
            string.Create(CultureInfo.InvariantCulture, $"{tally.Covered} files match a creation rule: {tally.Covered - tally.Different - tally.Unreadable} consistent, {tally.Different} differ, {tally.Unreadable} unreadable. {tally.Uncovered} other SOPS files are not covered by a rule, {tally.Unjudgeable} entries cannot be judged, {tally.Changed} have changed recipients."),
            cancellationToken).ConfigureAwait(false);
        await EscapedOutput.Line(output, ProofNote, cancellationToken).ConfigureAwait(false);
        if (tally.Covered == 0)
        {
            await EscapedOutput.Line(error, "No file matches a creation rule, so nothing was verified.", cancellationToken).ConfigureAwait(false);
            return ExitCodes.Failure;
        }

        if (tally.Unreadable > 0 || tally.Unjudgeable > 0)
        {
            return ExitCodes.Failure;
        }

        return tally.Different > 0 || (tally.Uncovered > 0 && !options.AllowUnverifiedFiles) || (tally.Changed > 0 && !options.AllowChangedRecipients)
            ? ExitCodes.Differences
            : ExitCodes.Success;
    }

    private static async Task Examine(
        ScanEntry entry,
        string root,
        SopsCreationRuleSet rules,
        GitBase? baseline,
        Tally tally,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var relative = entry.RelativePath;
        if (entry.Kind == ScanEntryKind.UnreadableDirectory)
        {
            tally.Unjudgeable++;
            await EscapedOutput.Line(error, $"{relative}: {entry.Problem}", cancellationToken).ConfigureAwait(false);
            return;
        }

        var rule = rules.FirstMatchPath(relative);
        var named = Path.GetFileName(relative).Contains(".sops.", StringComparison.OrdinalIgnoreCase);
        if (entry.Kind == ScanEntryKind.Link)
        {
            if (rule is not null)
            {
                tally.Unjudgeable++;
                await EscapedOutput.Line(error, $"{relative}: a symbolic link cannot be checked", cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        if (relative == SopsConfigurationName || !(named || SopsSniffer.IsCandidateExtension(relative)))
        {
            return;
        }

        var inScope = rule is not null || named;
        if (relative.Any(static c => char.IsControl(c) || c is ':' or '\\') && inScope)
        {
            tally.Unjudgeable++;
            await EscapedOutput.Line(error, $"{relative}: the path cannot be checked: it has a control character, ':' or '\\'", cancellationToken).ConfigureAwait(false);
            return;
        }

        var content = await ReadFile(entry, cancellationToken).ConfigureAwait(false);
        if (content.Problem is not null)
        {
            if (inScope)
            {
                tally.Unjudgeable++;
                await EscapedOutput.Line(error, $"{relative}: {content.Problem}", cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        if (!named && !SopsSniffer.LooksLikeSops(relative, content.Text))
        {
            return;
        }

        if (rule is null)
        {
            tally.Uncovered++;
            await EscapedOutput.Line(output, $"{relative}: no creation rule matches, not verified", cancellationToken).ConfigureAwait(false);
            return;
        }

        tally.Covered++;
        var format = SopsSniffer.FormatOf(relative);
        var holders = format is null
            ? Result.Error<SopsFileRecipients>("Only YAML and JSON SOPS files have readable recipients.")
            : SopsMetadataReader.Read(content.Text, format.Value);
        if (holders.IsFailure)
        {
            tally.Unreadable++;
            await EscapedOutput.Line(error, $"{relative}: {holders.ErrorDetails?.Detail}", cancellationToken).ConfigureAwait(false);
            return;
        }

        var verification = RecipientVerification.Compare(holders.Value, rule);
        if (!verification.IsExact)
        {
            tally.Different++;
        }

        await RecipientsCommand.Report(output, relative, verification, cancellationToken).ConfigureAwait(false);
        if (baseline is not null)
        {
            tally.Changed += await ReportChanges(baseline, relative, holders.Value, output, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<int> ReportChanges(GitBase baseline, string relative, SopsFileRecipients current, TextWriter output, CancellationToken cancellationToken)
    {
        var before = await baseline.ReadHolders(relative, cancellationToken).ConfigureAwait(false);
        var changes = 0;
        foreach (var (label, enc) in current.Envelopes.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            string? reason = null;
            if (before is null || !before.Envelopes.TryGetValue(label, out var previous))
            {
                reason = "is new";
            }
            else if (previous != enc)
            {
                reason = "has a different enc";
            }

            if (reason is not null)
            {
                changes++;
                await EscapedOutput.Line(output, $"{relative}: changed: needs review: recipient {label} {reason}", cancellationToken).ConfigureAwait(false);
            }
        }

        return changes == 0 ? 0 : 1;
    }

    private static async Task<(string Text, string? Problem)> ReadFile(ScanEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            if (new FileInfo(entry.AbsolutePath).Length > MaxFileBytes)
            {
                return (string.Empty, "the file is larger than 16 MiB");
            }


            // Decode the bytes ourselves: a stream reader would strip a byte order mark, which the reader must see and refuse.
            var bytes = await File.ReadAllBytesAsync(entry.AbsolutePath, cancellationToken).ConfigureAwait(false);
            return (new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes), null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return (string.Empty, "the file cannot be read");
        }
    }

    private sealed class Tally
    {
        public int Covered
        {
            get; set;
        }

        public int Uncovered
        {
            get; set;
        }

        public int Different
        {
            get; set;
        }

        public int Unreadable
        {
            get; set;
        }

        public int Unjudgeable
        {
            get; set;
        }

        public int Changed
        {
            get; set;
        }
    }
}
