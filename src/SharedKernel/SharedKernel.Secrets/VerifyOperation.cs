using System.Globalization;
using System.Text;
using PandaBear.Results;

namespace PandaBear.Secrets;

/// <summary>
/// Verifies a repository: every SOPS-encrypted file in the repository, whatever its name or directory, is judged against the first creation rule
/// whose path expression matches it. A file passes when its labels are consistent with the rule, which proves labels and structure and not who can
/// decrypt. It starts no process unless <c>--base</c> asks for git. Verifying nothing, a path or entry that cannot be judged, and a file that no
/// rule covers are not success.
/// </summary>
internal static class VerifyOperation
{
    /// <summary>The note printed once per run, so a pass is not read as more than it proves.</summary>
    public const string ProofNote = "Note: this proves labels and structure, not who can decrypt. Compare with a base revision (--base) or trial-decrypt (recipients check --prove-with-identity) to go further.";

    private const string SopsConfigurationName = ".sops.yaml";

    private static readonly string[] ProbeNames =
    [
        "/", "/a", "/a.sops.yaml", "/a.sops.yml", "/a.sops.json", "/a.yaml", "/a.yml", "/a.json", "/a/b.sops.yaml", "/a/b.sops.json", "/a/b",
    ];

    /// <summary>
    /// Returns 0 when every file is covered, consistent, and (with <c>--base</c>) unchanged, 1 when one differs, is weakened, is uncovered, or has a changed
    /// recipient, and 2 when nothing was verified or an entry cannot be read or judged. A failure outranks a difference.
    /// </summary>
    /// <exception cref="InvalidOperationException">The configuration is invalid or git cannot be used.</exception>
    public static async Task<int> Run(
        string root,
        SecretsPrograms programs,
        VerifySettings options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var rules = await SecretsConfigurationFile.Read(root, cancellationToken).ConfigureAwait(false);
        var baseline = options.BaseRevision is null ? null : await GitBase.Open(programs, root, options.BaseRevision, cancellationToken).ConfigureAwait(false);
        Tally tally = new();
        foreach (var entry in SopsFileFinder.Find(root))
        {
            await Examine(programs, entry, rules, baseline, tally, output, error, cancellationToken).ConfigureAwait(false);
        }

        await EscapedOutput.Line(
            output,
            string.Create(CultureInfo.InvariantCulture, $"{tally.Covered} files match a creation rule: {tally.Covered - tally.Different - tally.Unreadable} consistent, {tally.Different} differ, {tally.Unreadable} unreadable. {tally.Uncovered} other SOPS files are not covered by a rule, {tally.Unjudgeable} entries cannot be judged, {tally.Changed} have changed recipients."),
            cancellationToken).ConfigureAwait(false);
        await EscapedOutput.Line(output, ProofNote, cancellationToken).ConfigureAwait(false);
        if (tally.Covered == 0)
        {
            await EscapedOutput.Line(error, "No file matches a creation rule, so nothing was verified.", cancellationToken).ConfigureAwait(false);
            return SecretsExitCodes.Failure;
        }

        if (tally.Unreadable > 0 || tally.Unjudgeable > 0)
        {
            return SecretsExitCodes.Failure;
        }

        return tally.Different > 0 || (tally.Uncovered > 0 && !options.AllowUnverifiedFiles) || (tally.Changed > 0 && !options.AllowChangedRecipients)
            ? SecretsExitCodes.Differences
            : SecretsExitCodes.Success;
    }

    private static async Task Examine(
        SecretsPrograms programs,
        ScanEntry entry,
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
            await ExamineLink(relative, rules, named, tally, output, error, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (relative == SopsConfigurationName)
        {
            return;
        }

        if (rule is not null && !named)
        {
            // The structural rule: nothing in a rule's scope is skipped because of what it contains. A file that is not named *.sops.* is not judged, and that fails the run.
            tally.Unjudgeable++;
            await EscapedOutput.Line(error, $"{relative}: not judged: name does not mark it as SOPS-encrypted (rename it to *.sops.<ext>, or move it out of the rule's scope)", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (rule is null && !(named || SopsSniffer.IsCandidateExtension(relative)))
        {
            return;
        }

        var (bytes, problem) = await GuardedFileReader.Read(entry.AbsolutePath, SopsFileLimits.MaxBytes, programs.FileReadTimeout, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            if (named)
            {
                tally.Unjudgeable++;
                await EscapedOutput.Line(error, $"{relative}: {problem}", cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        if (rule is null)
        {
            // Best effort outside every rule: a file that looks like SOPS (by a heuristic that cannot be complete) is listed as uncovered.
            var sniffed = SopsSniffer.Classify(bytes, out _, out _);
            if (named || sniffed == SniffResult.Sops)
            {
                tally.Uncovered++;
                await EscapedOutput.Line(output, $"{relative}: no creation rule matches, not verified", cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        if (relative.Any(static c => char.IsControl(c) || c is ':' or '\\'))
        {
            tally.Unjudgeable++;
            await EscapedOutput.Line(error, $"{relative}: the path cannot be checked: it has a control character, ':' or '\\'", cancellationToken).ConfigureAwait(false);
            return;
        }

        tally.Covered++;
        var format = SopsSniffer.FormatOf(relative);
        var holders = format is null
            ? Result.Error<SopsFileRecipients>("Only YAML and JSON SOPS files have readable recipients.")
            : SopsMetadataReader.Read(new UTF8Encoding(false).GetString(bytes), format.Value);
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

        await RecipientsOperation.Report(output, relative, verification, cancellationToken).ConfigureAwait(false);
        if (baseline is not null)
        {
            tally.Changed += await ReportChanges(baseline, relative, holders.Value, output, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A symbolic link is never followed, so what is behind it is never checked. One that sits where a rule applies, or that a rule could apply under
    /// (a directory link whose children a rule matches), is unjudgeable. One elsewhere that is named like a SOPS file is listed as uncovered.
    /// </summary>
    private static async Task ExamineLink(string relative, SopsCreationRuleSet rules, bool named, Tally tally, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (rules.FirstMatchPath(relative) is not null || ProbeNames.Any(probe => rules.FirstMatchPath(relative + probe) is not null))
        {
            tally.Unjudgeable++;
            await EscapedOutput.Line(error, $"{relative}: a symbolic link cannot be checked", cancellationToken).ConfigureAwait(false);
        }
        else if (named)
        {
            tally.Uncovered++;
            await EscapedOutput.Line(output, $"{relative}: no creation rule matches, not verified", cancellationToken).ConfigureAwait(false);
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
