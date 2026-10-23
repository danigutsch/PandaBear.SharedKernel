using PandaBear.VaultManager.Tool.Analysis;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Brief;

/// <summary>
/// Makes the brief of a merge: the groups that need a decision, their members, the fields on which the members differ,
/// and the members' secret findings. A value appears only for names, folders, user names and website addresses as the
/// inputs hold them, with user names that hold a secret finding withheld and addresses masked as in the catalog, and notes
/// that the user shared for exactly their text and that hold no secret finding. Secret fields show flags only, and
/// findings show where they lie, never what they hold.
/// </summary>
internal static class BriefBuilder
{
    /// <summary>Makes the brief of a merge.</summary>
    /// <param name="inputs">Input files the merge read, with their SHA-256.</param>
    /// <param name="scanned">Vault the merge read and scanned.</param>
    /// <param name="result">Result of merging <paramref name="scanned"/> with <paramref name="decisions"/>.</param>
    /// <param name="decisions">The user's decisions the merge ran with; notes are shown only when they share them.</param>
    /// <returns>The brief.</returns>
    public static BriefDocument Build(IReadOnlyList<BriefInput> inputs, ScannedVault scanned, PipelineResult result, MergeDecisions decisions)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(scanned);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(decisions);

        var context = new Context(scanned, result, decisions);
        IReadOnlyList<IReadOnlyList<PasswordCheck>> passwords = PasswordHealthReport.CheckMembers([.. result.Match.Groups.Select(group => group.Members)]);
        var groups = new List<BriefGroup>();
        for (int index = 0; index < result.Match.Groups.Count; index++)
        {
            MatchGroup group = result.Match.Groups[index];
            if (context.UnresolvedGroups.Contains(group.Key) || group.MemberKeys.Any(context.PendingFindingItems.Contains))
            {
                VaultItemType type = result.Merged.Items[index].Type;
                groups.Add(new BriefGroup(group.Key, type, Members(group, context), Fields(group, type, passwords[index], context)));
            }
        }

        var summary = new BriefSummary(
            result.Merged.Items.Count,
            result.MergeReport.MergedItems,
            result.MergeReport.AutoResolvedConflicts,
            result.Unresolved.Count,
            result.Findings.Sum(item => item.Findings.Count),
            result.Findings.Sum(item => item.AcceptedFindingIds.Count),
            result.PendingFindingCount,
            result.PasswordHealth.Issues.Count,
            result.AttachmentUploadCount);
        return new BriefDocument(BriefFile.CurrentVersion, inputs, summary, groups, result.Match.Suggestions);
    }

    private static List<BriefMember> Members(MatchGroup group, Context context)
    {
        var members = new List<BriefMember>(group.Members.Count);
        for (int index = 0; index < group.Members.Count; index++)
        {
            VaultItem member = group.Members[index];
            string originKey = group.MemberKeys[index];
            members.Add(new BriefMember(
                originKey,
                member.Origin.SourceFile,
                member.Name,
                member.FolderPath,
                context.Addresses(member, originKey).Select(ItemNormalizer.Host).FirstOrDefault(host => host is not null && !host.Contains(ShownValues.Mask, StringComparison.Ordinal)),
                context.Username(member, originKey),
                member.LastModified,
                IsNewest(member, group.Members),
                [.. context.FindingsOf(originKey).Select(finding => Finding(finding, originKey, context))]));
        }

        return members;
    }

    // Newest only when every member has a modification time and one alone has the latest.
    private static bool IsNewest(VaultItem member, IReadOnlyList<VaultItem> members)
    {
        if (members.Any(item => item.LastModified is null))
        {
            return false;
        }

        DateTimeOffset? latest = members.Max(item => item.LastModified);
        return member.LastModified == latest && members.Count(item => item.LastModified == latest) == 1;
    }

    private static BriefFinding Finding(SecretFinding finding, string originKey, Context context) => new(
        finding.Id,
        finding.Kind,
        finding.Confidence,
        finding.Location.FieldPath,
        Line(context.SourceOf(originKey), finding.Location),
        finding.ProposedTarget.ToString(),
        finding.IsPreAccepted,
        context.PendingFindings.Contains((originKey, finding.Id)),
        finding.IsPreAccepted || (!finding.NeedsReview && !MovesIntoShownPlace(finding) && !ChangesMatching(finding)),
        !finding.IsPreAccepted || !ChangesMatching(finding));

    // Catalogs and briefs show user names, website addresses, and text fields, so text moved there is no longer withheld.
    private static bool MovesIntoShownPlace(SecretFinding finding) =>
        finding.ProposedTarget.Kind is PlacementTargetKind.LoginUsername or PlacementTargetKind.LoginUris or PlacementTargetKind.LoginFromConnection or PlacementTargetKind.TextField;

    // Matching uses hosts, user names, titles, and types, and placement runs before it: a finding that fills or edits a user
    // name or an address, or that makes a new item, changes which items match.
    private static bool ChangesMatching(SecretFinding finding) =>
        finding.ProposedTarget.Kind is PlacementTargetKind.LoginUsername or PlacementTargetKind.LoginUris or PlacementTargetKind.LoginFromConnection
            or PlacementTargetKind.NewSshKeyItem or PlacementTargetKind.NewCardItem
        || string.Equals(finding.Location.FieldPath, SecretLocation.LoginUsername, StringComparison.Ordinal);

    // Lines are split on line feeds, as the scanner splits them.
    private static int? Line(ScannedItem? source, SecretLocation location)
    {
        string? text = source is null ? null : TextOf(source, location.FieldPath);
        return text is null || location.Start < 0 || location.Start > text.Length
            ? null
            : 1 + text.AsSpan(0, location.Start).Count('\n');
    }

    private static string? TextOf(ScannedItem source, string fieldPath)
    {
        VaultItem item = source.Source.Item;
        if (string.Equals(fieldPath, SecretLocation.Notes, StringComparison.Ordinal))
        {
            return item.Notes;
        }

        if (fieldPath.StartsWith(SecretLocation.AttachmentPrefix, StringComparison.Ordinal))
        {
            return source.AttachmentTexts.GetValueOrDefault(fieldPath);
        }

        if (SecretLocation.TryParseField(fieldPath, out int index, out bool isName))
        {
            return index < item.Fields.Count ? (isName ? item.Fields[index].Name : item.Fields[index].Value) : null;
        }

        return ScalarFields.All.FirstOrDefault(field => string.Equals(field.Path, fieldPath, StringComparison.Ordinal))?.Get(item);
    }

    private static List<BriefField> Fields(MatchGroup group, VaultItemType type, IReadOnlyList<PasswordCheck> passwords, Context context)
    {
        IReadOnlyList<VaultItem> members = group.Members;
        var fields = new List<BriefField>();
        if (members.Count < 2)
        {
            return fields;
        }

        if (members.Select(member => ItemNormalizer.Title(member.Name)).Distinct(StringComparer.Ordinal).Skip(1).Any())
        {
            fields.Add(Field(group, FieldPaths.Name, decidable: true, _ => true, member => ItemNormalizer.Title(member.Name), (member, _, _) => new() { Value = member.Name }, context));
        }

        if (members.Select(member => member.FolderPath).Where(folder => !string.IsNullOrWhiteSpace(folder)).Distinct(StringComparer.Ordinal).Skip(1).Any())
        {
            fields.Add(Field(group, FieldPaths.Folder, decidable: true, member => !string.IsNullOrWhiteSpace(member.FolderPath), member => member.FolderPath!, (member, _, _) => new() { Value = member.FolderPath }, context));
        }

        foreach (ScalarField scalar in ScalarFields.All.Where(field => field.AppliesTo(type)))
        {
            if (members.Select(member => FieldDiff.ScalarKey(scalar, member)).Where(key => key.Length > 0).Distinct(StringComparer.Ordinal).Skip(1).Any())
            {
                fields.Add(Field(group, scalar.Path, decidable: true, member => FieldDiff.ScalarKey(scalar, member).Length > 0, member => FieldDiff.ScalarKey(scalar, member), (member, index, originKey) => scalar.Path switch
                {
                    FieldPaths.Notes => new() { Value = context.SharedNotes(originKey) },
                    FieldPaths.LoginUsername => new() { Value = context.Username(member, originKey) },
                    FieldPaths.LoginPassword => new() { Weak = passwords[index].Weak, Reused = passwords[index].Reused, InOtherHistory = passwords[index].InOtherHistory },
                    _ => new(),
                }, context));
            }
        }

        IReadOnlyList<string> changed = FieldDiff.ChangedPaths(members);
        if (changed.Contains(FieldPaths.LoginUris))
        {
            fields.Add(Field(group, FieldPaths.LoginUris, decidable: false, member => member.Login?.Uris.Count > 0, null, (member, _, originKey) => new() { Values = context.Addresses(member, originKey) }, context));
        }

        if (changed.Contains(FieldPaths.LoginFido2Credentials))
        {
            fields.Add(Field(group, FieldPaths.LoginFido2Credentials, decidable: false, member => member.Login?.Fido2Credentials.Count > 0, null, (_, _, _) => new(), context));
        }

        if (changed.Contains(FieldPaths.Fields))
        {
            fields.Add(Field(group, FieldPaths.Fields, decidable: false, member => member.Fields.Count > 0, null, (_, _, _) => new(), context));
        }

        if (changed.Contains(FieldPaths.PasswordHistory))
        {
            fields.Add(Field(group, FieldPaths.PasswordHistory, decidable: false, member => member.PasswordHistory.Count > 0, null, (_, _, _) => new(), context));
        }

        return [.. fields.OrderBy(field => FieldDiff.ReportOrder(field.FieldPath))];
    }

    private static BriefField Field(
        MatchGroup group,
        string fieldPath,
        bool decidable,
        Func<VaultItem, bool> holds,
        Func<VaultItem, string>? valueKey,
        Func<VaultItem, int, string, Shown> show,
        Context context)
    {
        int[] holderIndexes = [.. Enumerable.Range(0, group.Members.Count).Where(index => holds(group.Members[index]))];
        HashSet<int> newest = valueKey is null ? [] : NewestValueHolders(holderIndexes, group.Members, valueKey);
        var holders = new List<BriefHolder>(holderIndexes.Length);
        foreach (int index in holderIndexes)
        {
            Shown shown = show(group.Members[index], index, group.MemberKeys[index]);
            holders.Add(new BriefHolder(group.MemberKeys[index], newest.Contains(index), shown.Value, shown.Values, shown.Weak, shown.Reused, shown.InOtherHistory));
        }

        return new BriefField(fieldPath, context.UnresolvedFields.Contains((group.Key, fieldPath)), decidable, holders);
    }

    // The holders of the value that the newest policy takes: every holder has a modification time, and the newest holder of
    // that value is newer than the newest holder of every other value.
    private static HashSet<int> NewestValueHolders(int[] holderIndexes, IReadOnlyList<VaultItem> members, Func<VaultItem, string> valueKey)
    {
        if (holderIndexes.Length == 0 || holderIndexes.Any(index => members[index].LastModified is null))
        {
            return [];
        }

        var byValue = holderIndexes
            .GroupBy(index => valueKey(members[index]), StringComparer.Ordinal)
            .Select(value => (Holders: value.ToArray(), Newest: value.Max(index => members[index].LastModified)))
            .OrderByDescending(value => value.Newest)
            .ToArray();
        return byValue.Length > 1 && byValue[0].Newest == byValue[1].Newest ? [] : [.. byValue[0].Holders];
    }

    // What a holder shows besides its origin key: a value that may be shown, or the password flags.
    private sealed record Shown
    {
        public string? Value { get; init; }

        public IReadOnlyList<string>? Values { get; init; }

        public bool? Weak { get; init; }

        public bool? Reused { get; init; }

        public bool? InOtherHistory { get; init; }
    }

    // Lookups shared by the groups of one brief.
    private sealed class Context
    {
        private readonly Dictionary<string, ScannedItem> sources = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ItemFindingsResult> findings = new(StringComparer.Ordinal);
        private readonly MergeDecisions decisions;

        public Context(ScannedVault scanned, PipelineResult result, MergeDecisions decisions)
        {
            this.decisions = decisions;
            foreach (ScannedItem item in scanned.Items)
            {
                sources.TryAdd(item.Source.OriginKey, item);
            }

            foreach (ItemFindingsResult item in result.Findings)
            {
                findings.TryAdd(item.OriginKey, item);
            }

            UnresolvedGroups = [.. result.Unresolved.Select(conflict => conflict.GroupKey)];
            UnresolvedFields = [.. result.Unresolved.Select(conflict => (conflict.GroupKey, conflict.FieldPath))];
            PendingFindings = [.. result.PendingFindings.SelectMany(item => item.Choices.Select(choice => (item.OriginKey, choice.FindingId)))];
            PendingFindingItems = [.. result.PendingFindings.Select(item => item.OriginKey)];
        }

        public HashSet<string> UnresolvedGroups { get; }

        public HashSet<(string GroupKey, string FieldPath)> UnresolvedFields { get; }

        public HashSet<(string OriginKey, string FindingId)> PendingFindings { get; }

        public HashSet<string> PendingFindingItems { get; }

        public ScannedItem? SourceOf(string originKey) => sources.GetValueOrDefault(originKey);

        public IReadOnlyList<SecretFinding> FindingsOf(string originKey) =>
            findings.TryGetValue(originKey, out ItemFindingsResult? item) ? item.Findings : [];

        // Placement fills a login's user name and addresses from findings, such as a connection string, and those values are
        // findings. The merge sees the member after placement, so a user name or an address is shown only as the input holds
        // it, and an item that placement made shows none.
        public string? Username(VaultItem member, string originKey) =>
            SourceOf(originKey) is { } source
            && ShownValues.Username(source.Source.Item, FindingsOf(originKey)) is { } username
            && string.Equals(member.Login?.Username, username, StringComparison.Ordinal)
                ? username
                : null;

        public IReadOnlyList<string> Addresses(VaultItem member, string originKey)
        {
            if (SourceOf(originKey) is not { } source)
            {
                return [];
            }

            HashSet<string> asRead = [.. (source.Source.Item.Login?.Uris ?? []).Select(uri => uri.Value)];
            return [.. (member.Login?.Uris ?? []).Where(uri => asRead.Contains(uri.Value)).Select(uri => ShownValues.MaskUri(uri.Value))];
        }

        // Notes are shown only when the user shared exactly the notes as read and no finding lies in them, applied or not:
        // a finding that is pending, rejected, or that placement could not move stays in the text.
        public string? SharedNotes(string originKey) =>
            SourceOf(originKey)?.Source.Item.Notes is { } notes
            && decisions.SharesNotes(originKey, notes)
            && !FindingsOf(originKey).Any(finding => string.Equals(finding.Location.FieldPath, SecretLocation.Notes, StringComparison.Ordinal))
                ? notes
                : null;
    }
}
