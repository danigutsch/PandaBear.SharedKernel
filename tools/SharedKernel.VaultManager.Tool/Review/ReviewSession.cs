using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using PandaBear.VaultManager.Tool.Cli;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Merge;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Review;

/// <summary>
/// State of one review: the vault read and scanned once, the current decisions, and the merge they produce. Every change
/// of decisions merges again without writing anything. The display text of fields and findings masks secret values until
/// they are revealed; messages, exceptions, and <c>ToString</c> output hold names, keys, field paths, and counts, never
/// values.
/// </summary>
/// <remarks>
/// The policy is newest-wins, as for <c>merge</c> without <c>--policy</c>. Pending entries of the decisions read count as
/// no decision and are dropped; saving writes a fresh pending entry for each field and finding that still needs one.
/// </remarks>
internal sealed class ReviewSession
{
    private static readonly MergePolicy Policy = new();

    // Masked by default. Any other field of a member, such as its notes, user name, or custom fields, is masked too while a
    // finding of that member lies in it.
    private static readonly HashSet<string> SecretPaths = new(StringComparer.Ordinal)
    {
        FieldPaths.LoginPassword,
        FieldPaths.LoginTotp,
        FieldPaths.CardPrefix + "number",
        FieldPaths.CardPrefix + "code",
        FieldPaths.SshKeyPrefix + "privateKey",
        FieldPaths.PasswordHistory,
        FieldPaths.IdentityPrefix + "ssn",
        FieldPaths.IdentityPrefix + "passportNumber",
        FieldPaths.IdentityPrefix + "licenseNumber",
    };

    private readonly VaultCommandEnvironment environment;
    private readonly ScannedVault scanned;
    private readonly Dictionary<string, ScannedItem> sources = new(StringComparer.Ordinal);
    private readonly HashSet<(string GroupKey, string FieldPath)> revealedFields = [];
    private readonly HashSet<(string OriginKey, string FindingId)> revealedFindings = [];
    private readonly Dictionary<string, int> groupIndexes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ItemFindingsResult> findingsByItem = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> problemsByItem = new(StringComparer.Ordinal);
    private List<ReviewGroup> groups = [];

    /// <summary>Starts a review of a scanned vault and merges it with the given decisions.</summary>
    /// <param name="environment">Clock of the merge.</param>
    /// <param name="scanned">Vault read and scanned once.</param>
    /// <param name="decisions">Decisions to start from; pending entries count as no decision.</param>
    /// <param name="files">Decisions file and merged vault the review may write.</param>
    public ReviewSession(VaultCommandEnvironment environment, ScannedVault scanned, MergeDecisions decisions, ReviewFiles files)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(scanned);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(files);

        this.environment = environment;
        this.scanned = scanned;
        Files = files;
        foreach (ScannedItem item in scanned.Items)
        {
            sources.TryAdd(item.Source.OriginKey, item);
        }

        Decisions = decisions.WithPending([], []);
        Update();
    }

    /// <summary>Decisions file and merged vault the review may write.</summary>
    public ReviewFiles Files { get; }

    /// <summary>Current decisions, without pending entries.</summary>
    public MergeDecisions Decisions { get; private set; }

    /// <summary>Latest merge of the current decisions.</summary>
    public PipelineResult Result { get; private set; }

    /// <summary>Fields and findings that need a decision before the merged vault can be written.</summary>
    public PendingDecisions Pending { get; private set; } = PendingDecisions.None;

    /// <summary>Whether the decisions changed since they were read or saved.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>Whether this review saved the decisions file at least once.</summary>
    public bool Saved { get; private set; }

    /// <summary>Whether this review wrote the merged vault.</summary>
    public bool Written => WriteResult is not null;

    /// <summary>
    /// The merge that wrote the merged vault last, with the attachments to upload after the import and the writer's
    /// warnings, or <see langword="null"/> when this review wrote nothing.
    /// </summary>
    public PipelineResult? WriteResult { get; private set; }

    /// <summary>Number of match groups.</summary>
    public int GroupCount => groups.Count;

    /// <summary>Why the merged vault cannot be written, without values, or <see langword="null"/> when it can be written.</summary>
    public string? WriteBlocker =>
        Files.Output is null ? "No output file was given. Start the review with --output <file> to write the merged vault."
        : !Pending.IsEmpty ? string.Create(CultureInfo.InvariantCulture, $"{Pending.Fields.Count} field(s) and {Pending.FindingCount} finding(s) still need a decision. Nothing was written.")
        : Result.HasWriteErrors ? "Bitwarden would reject the import, so nothing was written: " + string.Join(" ", Result.WriteErrors.Select(ReviewText.OneLine))
        : null;

    /// <summary>Lists the groups that match a filter and a search.</summary>
    /// <param name="filter">Status filter.</param>
    /// <param name="search">
    /// Text that the name of the merged item or of a member contains, ignoring case and surrounding white space, or
    /// <see langword="null"/> for every group.
    /// </param>
    /// <returns>The groups, in group order.</returns>
    public IReadOnlyList<ReviewGroup> Groups(GroupFilter filter, string? search)
    {
        string text = search?.Trim() ?? string.Empty;
        return [.. groups.Where(group => Matches(group, filter) && (text.Length == 0 || Names(group).Any(name => name.Contains(text, StringComparison.OrdinalIgnoreCase))))];
    }

    /// <summary>Finds the group that holds an item.</summary>
    /// <param name="originKey">Origin key of the item.</param>
    /// <returns>The group, or <see langword="null"/> when no group holds the item.</returns>
    public ReviewGroup? GroupOf(string originKey) =>
        groups.FirstOrDefault(group => group.MemberKeys.Contains(originKey, StringComparer.Ordinal));

    /// <summary>Lists the members of a group.</summary>
    /// <param name="groupKey">Group key.</param>
    /// <returns>The members, in group order.</returns>
    /// <exception cref="ArgumentException">No group has the key.</exception>
    public IReadOnlyList<ReviewMember> Members(string groupKey)
    {
        MatchGroup group = RequireGroup(groupKey).Group;
        DateTimeOffset? newest = group.Members.Max(member => member.LastModified);
        bool single = newest is not null && group.Members.Count(member => member.LastModified == newest) == 1;
        return
        [
            .. group.Members.Select((member, index) => new ReviewMember(
                group.MemberKeys[index],
                member.Name,
                member.Origin.SourceFile,
                member.LastModified,
                single && member.LastModified == newest)),
        ];
    }

    /// <summary>
    /// Lists the fields of a group with each member's value: the name, the folder, the type when it differs, every
    /// single-value field of the merged type that a member holds or a decision names, and the lists. Secret values are
    /// masked unless the field is revealed.
    /// </summary>
    /// <param name="groupKey">Group key.</param>
    /// <returns>The fields, in report order.</returns>
    /// <exception cref="ArgumentException">No group has the key.</exception>
    public IReadOnlyList<ReviewField> Fields(string groupKey)
    {
        (MatchGroup group, VaultItem merged, ReviewGroup review) = RequireGroup(groupKey);
        var context = new FieldContext(
            group,
            review,
            [.. FieldDiff.ChangedPaths(group.Members)],
            Result.MergeReport.Groups.FirstOrDefault(report => string.Equals(report.GroupKey, groupKey, StringComparison.Ordinal)),
            group.Members.Count > 1);
        IReadOnlyList<VaultItem> members = group.Members;
        var rows = new List<RowData>();
        if (context.Changed.Contains(FieldPaths.Type))
        {
            rows.Add(new RowData(FieldPaths.Type, "Type", [.. members.Select(member => Plain(member.Type.ToString()))], AcceptsMember: false, AcceptsEmpty: false));
        }

        rows.Add(new RowData(FieldPaths.Name, "Name", [.. members.Select(member => Plain(member.Name))], AcceptsMember: true, AcceptsEmpty: false));
        rows.Add(new RowData(FieldPaths.Folder, "Folder", [.. members.Select(member => Plain(member.FolderPath))], AcceptsMember: true, AcceptsEmpty: false));
        foreach (ScalarField field in ScalarFields.All.Where(field => field.AppliesTo(merged.Type)))
        {
            Cell[] cells = [.. members.Select((member, index) => new Cell(field.Get(member), IsSecret(field.Path, group.MemberKeys[index])))];
            if (cells.Any(cell => !string.IsNullOrWhiteSpace(cell.Value)) || Decisions.Find(groupKey, field.Path) is not null)
            {
                rows.Add(new RowData(field.Path, field.Label, cells, AcceptsMember: true, AcceptsEmpty: true));
            }
        }

        if (merged.Type == VaultItemType.Login)
        {
            AddList(rows, context, FieldPaths.LoginUris, "Websites", [.. members.Select(Websites)]);
            AddList(rows, context, FieldPaths.LoginFido2Credentials, "Passkeys", [.. members.Select(member => Plain(Passkeys(member)))]);
        }

        AddList(rows, context, FieldPaths.Fields, "Custom fields", [.. members.Select((member, index) => CustomFields(member, group.MemberKeys[index]))]);
        AddList(rows, context, FieldPaths.PasswordHistory, "Password history", [.. members.Select(History)]);
        return [.. rows.Select(row => ToField(context, row))];
    }

    /// <summary>Lists the secret findings of the members of a group. Values are masked unless the finding is revealed.</summary>
    /// <param name="groupKey">Group key.</param>
    /// <returns>The findings, by member and then in the order the scan found them.</returns>
    /// <exception cref="ArgumentException">No group has the key.</exception>
    public IReadOnlyList<ReviewFinding> Findings(string groupKey)
    {
        MatchGroup group = RequireGroup(groupKey).Group;
        var rows = new List<ReviewFinding>();
        foreach (string originKey in group.MemberKeys)
        {
            if (!findingsByItem.TryGetValue(originKey, out ItemFindingsResult? item))
            {
                continue;
            }

            foreach (SecretFinding finding in item.Findings)
            {
                bool revealed = revealedFindings.Contains((originKey, finding.Id));
                bool accepted = item.AcceptedFindingIds.Contains(finding.Id, StringComparer.Ordinal);
                string? problem = accepted ? ProblemOf(originKey, finding.Id) : null;
                rows.Add(new ReviewFinding(
                    originKey,
                    item.ItemName,
                    finding.Id,
                    finding.Kind,
                    finding.Confidence,
                    finding.ProposedTarget.ToString(),
                    finding.Message,
                    finding.IsPreAccepted,
                    finding.NeedsReview,
                    Decisions.FindFinding(originKey, finding.Id),
                    accepted && problem is null,
                    problem,
                    revealed,
                    revealed ? ReviewText.OneLine(FindingText(originKey, finding)) : ReviewText.Mask));
            }
        }

        return rows;
    }

    /// <summary>Takes one member's value for a field.</summary>
    /// <param name="groupKey">Group key.</param>
    /// <param name="fieldPath">Field path: the name, the folder, or a single-value field of the merged type.</param>
    /// <param name="originKey">Origin key of the member.</param>
    /// <exception cref="ArgumentException">No group has the key, the item is not a member, or the field takes no member choice.</exception>
    public void ChooseMember(string groupKey, string fieldPath, string originKey)
    {
        ArgumentNullException.ThrowIfNull(fieldPath);
        (MatchGroup group, VaultItem merged, _) = RequireGroup(groupKey);
        RequireMember(group, originKey);
        if (!AcceptsMember(group, merged.Type, fieldPath))
        {
            throw new ArgumentException($"Field {fieldPath} of group {groupKey} takes no member choice.", nameof(fieldPath));
        }

        SetChoices(groupKey, [FieldChoice.Member(fieldPath, originKey)]);
    }

    /// <summary>Takes one member's value for every conflicting field of a group.</summary>
    /// <param name="groupKey">Group key.</param>
    /// <param name="originKey">Origin key of the member.</param>
    /// <returns>Number of fields decided; zero when no field conflicts.</returns>
    /// <exception cref="ArgumentException">No group has the key, or the item is not a member.</exception>
    public int ChooseMemberForConflicts(string groupKey, string originKey)
    {
        (MatchGroup group, _, ReviewGroup review) = RequireGroup(groupKey);
        RequireMember(group, originKey);
        if (review.ConflictingFields.Count == 0)
        {
            return 0;
        }

        SetChoices(groupKey, [.. review.ConflictingFields.Select(path => FieldChoice.Member(path, originKey))]);
        return review.ConflictingFields.Count;
    }

    /// <summary>Drops a field from every member of a group.</summary>
    /// <param name="groupKey">Group key.</param>
    /// <param name="fieldPath">Field path: a single-value field of the merged type, or a list.</param>
    /// <exception cref="ArgumentException">No group has the key, or the field cannot be dropped.</exception>
    public void ChooseEmpty(string groupKey, string fieldPath)
    {
        ArgumentNullException.ThrowIfNull(fieldPath);
        (MatchGroup group, VaultItem merged, _) = RequireGroup(groupKey);
        if (!AcceptsEmpty(group, merged.Type, fieldPath))
        {
            throw new ArgumentException($"Field {fieldPath} of group {groupKey} cannot be dropped.", nameof(fieldPath));
        }

        SetChoices(groupKey, [FieldChoice.Empty(fieldPath)]);
    }

    /// <summary>Removes the decision on a field, so the policy resolves it again. Does nothing when there is none.</summary>
    /// <param name="groupKey">Group key.</param>
    /// <param name="fieldPath">Field path.</param>
    public void ClearChoice(string groupKey, string fieldPath)
    {
        ArgumentNullException.ThrowIfNull(groupKey);
        ArgumentNullException.ThrowIfNull(fieldPath);
        if (Decisions.Find(groupKey, fieldPath) is null)
        {
            return;
        }

        Change(Decisions with { Groups = [.. WithoutChoices(groupKey, [fieldPath]).Where(group => group.Choices.Count > 0)] });
    }

    /// <summary>Accepts or rejects a finding.</summary>
    /// <param name="originKey">Origin key of the item that holds the finding.</param>
    /// <param name="findingId">Finding identifier.</param>
    /// <param name="decision"><see cref="FindingDecision.Accepted"/> or <see cref="FindingDecision.Rejected"/>.</param>
    /// <exception cref="ArgumentException">The item holds no such finding, or the decision is <see cref="FindingDecision.Pending"/>.</exception>
    public void DecideFinding(string originKey, string findingId, FindingDecision decision)
    {
        if (decision is not (FindingDecision.Accepted or FindingDecision.Rejected))
        {
            throw new ArgumentException("A finding decision accepts or rejects the finding.", nameof(decision));
        }

        ItemFindingsResult item = RequireFinding(originKey, findingId);
        List<ItemFindingDecisions> items = WithoutFinding(originKey, findingId);
        var choice = new FindingChoice(findingId, decision);
        int index = items.FindIndex(existing => string.Equals(existing.OriginKey, originKey, StringComparison.Ordinal));
        if (index < 0)
        {
            items.Add(new ItemFindingDecisions(originKey, [choice], item.ItemName));
        }
        else
        {
            items[index] = items[index] with { Choices = [.. items[index].Choices, choice], ItemName = items[index].ItemName ?? item.ItemName };
        }

        Change(Decisions with { Findings = items });
    }

    /// <summary>Removes the decision on a finding. Does nothing when there is none.</summary>
    /// <param name="originKey">Origin key of the item that holds the finding.</param>
    /// <param name="findingId">Finding identifier.</param>
    public void ClearFinding(string originKey, string findingId)
    {
        ArgumentNullException.ThrowIfNull(originKey);
        ArgumentNullException.ThrowIfNull(findingId);
        if (Decisions.FindFinding(originKey, findingId) is null)
        {
            return;
        }

        Change(Decisions with { Findings = [.. WithoutFinding(originKey, findingId).Where(item => item.Choices.Count > 0)] });
    }

    /// <summary>
    /// Links two items, so their groups merge into one. A split between the two items is removed; other splits still keep
    /// the members of the two groups apart.
    /// </summary>
    /// <param name="originKey">Origin key of one item.</param>
    /// <param name="otherOriginKey">Origin key of the other item.</param>
    /// <returns><see langword="true"/> when both items are in one group afterwards.</returns>
    /// <exception cref="ArgumentException">An item is in no group, or both keys name the same item.</exception>
    public bool Link(string originKey, string otherOriginKey)
    {
        ReviewGroup group = RequireItem(originKey);
        ReviewGroup other = RequireItem(otherOriginKey);
        if (string.Equals(originKey, otherOriginKey, StringComparison.Ordinal))
        {
            throw new ArgumentException("An item cannot be linked with itself.", nameof(otherOriginKey));
        }

        if (ReferenceEquals(group, other))
        {
            return true;
        }

        MatchOverrides overrides = Decisions.Overrides;
        IReadOnlyList<OriginKeyPair> links = overrides.Links.Any(pair => SamePair(pair, originKey, otherOriginKey))
            ? overrides.Links
            : [.. overrides.Links, new OriginKeyPair(originKey, otherOriginKey)];
        Change(Decisions with { Overrides = new MatchOverrides(links, [.. overrides.Splits.Where(pair => !SamePair(pair, originKey, otherOriginKey))]) });
        return ReferenceEquals(GroupOf(originKey), GroupOf(otherOriginKey));
    }

    /// <summary>
    /// Splits an item out of its group: it may never share a group with the other members, and links between it and them
    /// are removed.
    /// </summary>
    /// <param name="originKey">Origin key of the item.</param>
    /// <exception cref="ArgumentException">The item is in no group.</exception>
    /// <exception cref="InvalidOperationException">The item is the only member of its group.</exception>
    public void Split(string originKey)
    {
        ReviewGroup group = RequireItem(originKey);
        string[] others = [.. group.MemberKeys.Where(key => !string.Equals(key, originKey, StringComparison.Ordinal))];
        if (others.Length == 0)
        {
            throw new InvalidOperationException($"{originKey} is the only member of its group.");
        }

        MatchOverrides overrides = Decisions.Overrides;
        OriginKeyPair[] splits =
        [
            .. overrides.Splits,
            .. others.Where(other => !overrides.Splits.Any(pair => SamePair(pair, originKey, other))).Select(other => new OriginKeyPair(originKey, other)),
        ];
        OriginKeyPair[] links = [.. overrides.Links.Where(pair => !others.Any(other => SamePair(pair, originKey, other)))];
        Change(Decisions with { Overrides = new MatchOverrides(links, splits) });
    }

    /// <summary>Reveals or masks the secret values of a field of a group.</summary>
    /// <param name="groupKey">Group key.</param>
    /// <param name="fieldPath">Field path.</param>
    /// <returns><see langword="true"/> when the field is revealed; <see langword="false"/> when it is masked.</returns>
    public bool ToggleReveal(string groupKey, string fieldPath)
    {
        ArgumentNullException.ThrowIfNull(groupKey);
        ArgumentNullException.ThrowIfNull(fieldPath);
        return Toggle(revealedFields, (groupKey, fieldPath));
    }

    /// <summary>Reveals or masks the value of a finding.</summary>
    /// <param name="originKey">Origin key of the item that holds the finding.</param>
    /// <param name="findingId">Finding identifier.</param>
    /// <returns><see langword="true"/> when the finding is revealed; <see langword="false"/> when it is masked.</returns>
    public bool ToggleRevealFinding(string originKey, string findingId)
    {
        ArgumentNullException.ThrowIfNull(originKey);
        ArgumentNullException.ThrowIfNull(findingId);
        return Toggle(revealedFindings, (originKey, findingId));
    }

    /// <summary>
    /// Saves the decisions to the decisions file, readable only by its owner on Unix, with a pending entry for each field
    /// and finding that needs a decision.
    /// </summary>
    /// <exception cref="InvalidOperationException">The review has no decisions file.</exception>
    /// <exception cref="IOException">The file cannot be written.</exception>
    public void Save()
    {
        if (Files.DecisionsPath is not { } path)
        {
            throw new InvalidOperationException("No decisions file was given. Start the review with --decisions <file> to save decisions.");
        }

        DecisionsFile.Write(path, Decisions.WithPending(Pending.Fields, Pending.Findings));
        IsDirty = false;
        Saved = true;
    }

    /// <summary>
    /// Writes the merged vault: a Bitwarden password-protected export, or unencrypted JSON for a plaintext output. Nothing
    /// is written while a field or a proposed finding needs a decision, while the writer reports an error, over an input,
    /// or over an existing file unless the output allows replacing it.
    /// </summary>
    /// <param name="exportPassword">
    /// Password of an encrypted output, at least <see cref="PasswordReader.MinimumNewPasswordLength"/> characters; ignored
    /// for a plaintext output.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The review has no output file, something needs a decision, or the writer reports an error.
    /// </exception>
    /// <exception cref="ArgumentException">The password of an encrypted output is missing or too short.</exception>
    /// <exception cref="IOException">
    /// The output is an input, exists and may not be replaced, is a directory, is in a missing directory, or cannot be
    /// written.
    /// </exception>
    public void Write(string? exportPassword)
    {
        if (WriteBlocker is { } blocker)
        {
            throw new InvalidOperationException(blocker);
        }

        PipelineOutput output = Files.Output!;
        if (!output.Plaintext && (exportPassword is null || exportPassword.Length < PasswordReader.MinimumNewPasswordLength))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"The password for the new export needs at least {PasswordReader.MinimumNewPasswordLength} characters because it protects every secret in the file. Nothing was written."),
                nameof(exportPassword));
        }

        var writer = new VaultPipeline(environment with { ReadNewPassword = _ => exportPassword ?? string.Empty });
        PipelineResult written = writer.Merge(scanned, new MergeRequest { Policy = Policy, Decisions = Decisions, Output = output });
        if (!written.OutputWritten)
        {
            throw new InvalidOperationException("The merged vault was not written.");
        }

        WriteResult = written;
    }

    private static bool Matches(ReviewGroup group, GroupFilter filter) => filter switch
    {
        GroupFilter.All => true,
        GroupFilter.Pending => group.IsPending,
        GroupFilter.Conflict => group.ConflictingFields.Count > 0,
        GroupFilter.Suggested => group.IsSuggested,
        GroupFilter.Identical => group.Status == MatchStatus.Identical,
        GroupFilter.Unique => group.Status == MatchStatus.Unique,
        GroupFilter.HasFindings => group.Findings > 0,
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown group filter."),
    };

    private static IEnumerable<string> Names(ReviewGroup group) => group.MemberNames.Prepend(group.Name);

    private static bool SamePair(OriginKeyPair pair, string first, string second) =>
        (string.Equals(pair.First, first, StringComparison.Ordinal) && string.Equals(pair.Second, second, StringComparison.Ordinal))
        || (string.Equals(pair.First, second, StringComparison.Ordinal) && string.Equals(pair.Second, first, StringComparison.Ordinal));

    private static bool Toggle<T>(HashSet<T> set, T key)
    {
        if (set.Remove(key))
        {
            return false;
        }

        set.Add(key);
        return true;
    }

    private static void RequireMember(MatchGroup group, string originKey)
    {
        ArgumentNullException.ThrowIfNull(originKey);
        if (!group.MemberKeys.Contains(originKey, StringComparer.Ordinal))
        {
            throw new ArgumentException($"{originKey} is not a member of group {group.Key}.", nameof(originKey));
        }
    }

    // A member choice applies to the name, the folder, and the single-value fields of the merged item's type; an empty
    // choice applies to those single-value fields and to the lists. Groups with one member take no field decisions.
    private static bool AcceptsMember(MatchGroup group, VaultItemType type, string fieldPath) =>
        group.Members.Count > 1 && (fieldPath is FieldPaths.Name or FieldPaths.Folder || IsSingleValue(fieldPath, type));

    private static bool AcceptsEmpty(MatchGroup group, VaultItemType type, string fieldPath) =>
        group.Members.Count > 1 && (IsList(fieldPath) || IsSingleValue(fieldPath, type));

    private static bool IsSingleValue(string fieldPath, VaultItemType type) =>
        ScalarFields.All.Any(field => string.Equals(field.Path, fieldPath, StringComparison.Ordinal) && field.AppliesTo(type));

    private static bool IsList(string fieldPath) =>
        fieldPath is FieldPaths.Fields or FieldPaths.PasswordHistory or FieldPaths.LoginUris or FieldPaths.LoginFido2Credentials;

    private static List<string> ConflictingFields(IReadOnlyList<VaultItem> members, VaultItemType type)
    {
        if (members.Count < 2)
        {
            return [];
        }

        var paths = new List<string>();
        if (members.Select(member => ItemNormalizer.Title(member.Name)).Distinct(StringComparer.Ordinal).Skip(1).Any())
        {
            paths.Add(FieldPaths.Name);
        }

        if (members.Select(member => member.FolderPath ?? string.Empty).Distinct(StringComparer.Ordinal).Skip(1).Any())
        {
            paths.Add(FieldPaths.Folder);
        }

        foreach (ScalarField field in ScalarFields.All.Where(field => field.AppliesTo(type)))
        {
            if (members.Select(member => FieldDiff.ScalarKey(field, member)).Where(key => key.Length > 0).Distinct(StringComparer.Ordinal).Skip(1).Any())
            {
                paths.Add(field.Path);
            }
        }

        return paths;
    }

    private static Cell Plain(string? value) => new(value, Secret: false);

    private static string Passkeys(VaultItem member) => string.Join(
        ", ",
        (member.Login?.Fido2Credentials ?? []).Select(passkey => passkey.UserName is { Length: > 0 } user ? passkey.RpId + " (" + user + ")" : passkey.RpId));

    private static Cell History(VaultItem member)
    {
        if (member.PasswordHistory.Count == 0)
        {
            return new Cell(null, Secret: false);
        }

        return new Cell(
            string.Join(", ", member.PasswordHistory.Select(entry => entry.Password + " (" + Day(entry.LastUsedDate) + ")")),
            Secret: true,
            string.Join(", ", member.PasswordHistory.Select(entry => ReviewText.Mask + " (" + Day(entry.LastUsedDate) + ")")));
    }

    // Website addresses show their scheme, host, and path. User info, a query, and a fragment, which can hold credentials or
    // tokens, are masked until the field is revealed.
    private static Cell Websites(VaultItem member)
    {
        string[] uris = [.. (member.Login?.Uris ?? []).Select(uri => uri.Value)];
        string[] masked = [.. uris.Select(MaskUri)];
        return new Cell(string.Join(", ", uris), Secret: !uris.SequenceEqual(masked, StringComparer.Ordinal), string.Join(", ", masked));
    }

    private static string MaskUri(string uri)
    {
        int scheme = uri.IndexOf("://", StringComparison.Ordinal);
        int authorityStart = scheme < 0 ? 0 : scheme + 3;
        int authorityEnd = uri.IndexOfAny(['/', '?', '#'], authorityStart);
        authorityEnd = authorityEnd < 0 ? uri.Length : authorityEnd;
        int at = authorityEnd > authorityStart ? uri.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart) : -1;
        string authority = at < 0 ? uri[authorityStart..authorityEnd] : ReviewText.Mask + uri[at..authorityEnd];
        int rest = uri.IndexOfAny(['?', '#'], authorityEnd);
        string path = rest < 0 ? uri[authorityEnd..] : uri[authorityEnd..rest] + uri[rest] + ReviewText.Mask;
        return uri[..authorityStart] + authority + path;
    }

    private static string Day(DateTimeOffset time) => time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static void AddList(List<RowData> rows, FieldContext context, string fieldPath, string label, Cell[] cells)
    {
        if (cells.Any(cell => !string.IsNullOrWhiteSpace(cell.Value)) || context.Changed.Contains(fieldPath))
        {
            rows.Add(new RowData(fieldPath, label, cells, AcceptsMember: false, AcceptsEmpty: true));
        }
    }

    // A masked value shows as the mask, or as its own masked form, such as custom field names with masked values.
    private static string Display(Cell cell, bool revealed) =>
        !cell.Secret || revealed ? ReviewText.OneLine(cell.Value)
        : cell.Masked is { } masked ? ReviewText.OneLine(masked)
        : ReviewText.Value(cell.Value, masked: true);

    [MemberNotNull(nameof(Result))]
    private void Update()
    {
        Result = new VaultPipeline(environment).Merge(scanned, new MergeRequest { Policy = Policy, Decisions = Decisions });
        Pending = PendingDecisions.Find(Result, Decisions);
        findingsByItem.Clear();
        foreach (ItemFindingsResult item in Result.Findings)
        {
            findingsByItem.TryAdd(item.OriginKey, item);
        }

        problemsByItem.Clear();
        foreach (PlacementProblem problem in Result.PlacementProblems)
        {
            if (!problemsByItem.TryGetValue(problem.OriginKey, out List<string>? problems))
            {
                problems = [];
                problemsByItem.Add(problem.OriginKey, problems);
            }

            problems.Add(problem.Problem);
        }

        var pendingFields = Pending.Fields.GroupBy(field => field.GroupKey, StringComparer.Ordinal).ToDictionary(fields => fields.Key, fields => fields.Count(), StringComparer.Ordinal);
        var pendingFindings = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ItemFindingDecisions item in Pending.Findings)
        {
            pendingFindings[item.OriginKey] = pendingFindings.GetValueOrDefault(item.OriginKey) + item.Choices.Count;
        }

        HashSet<string> suggested = [.. Result.Match.Suggestions.SelectMany(suggestion => new[] { suggestion.FirstOriginKey, suggestion.SecondOriginKey })];
        groupIndexes.Clear();
        groups = new List<ReviewGroup>(Result.Match.Groups.Count);
        for (int index = 0; index < Result.Match.Groups.Count; index++)
        {
            MatchGroup group = Result.Match.Groups[index];
            VaultItem merged = Result.Merged.Items[index];
            groupIndexes.TryAdd(group.Key, index);
            groups.Add(new ReviewGroup(
                group.Key,
                merged.Name,
                merged.Type,
                group.Status,
                group.MemberKeys,
                [.. group.Members.Select(member => member.Name)],
                ConflictingFields(group.Members, merged.Type),
                pendingFields.GetValueOrDefault(group.Key),
                group.MemberKeys.Sum(key => findingsByItem.TryGetValue(key, out ItemFindingsResult? item) ? item.Findings.Count : 0),
                group.MemberKeys.Sum(key => pendingFindings.GetValueOrDefault(key)),
                group.MemberKeys.Any(suggested.Contains)));
        }
    }

    private void Change(MergeDecisions decisions)
    {
        Decisions = decisions;
        IsDirty = true;
        Update();
    }

    private void SetChoices(string groupKey, IReadOnlyList<FieldChoice> choices)
    {
        List<GroupDecision> updated = WithoutChoices(groupKey, [.. choices.Select(choice => choice.FieldPath)]);
        int index = updated.FindIndex(group => string.Equals(group.GroupKey, groupKey, StringComparison.Ordinal));
        if (index < 0)
        {
            updated.Add(new GroupDecision(groupKey, choices));
        }
        else
        {
            updated[index] = updated[index] with { Choices = [.. updated[index].Choices, .. choices] };
        }

        Change(Decisions with { Groups = updated });
    }

    // Removes the choices for some fields from every entry of a group, so the choice added next is the only one.
    private List<GroupDecision> WithoutChoices(string groupKey, IReadOnlyCollection<string> fieldPaths) =>
    [
        .. Decisions.Groups.Select(group => string.Equals(group.GroupKey, groupKey, StringComparison.Ordinal)
            ? group with { Choices = [.. group.Choices.Where(choice => !fieldPaths.Contains(choice.FieldPath, StringComparer.Ordinal))] }
            : group),
    ];

    private List<ItemFindingDecisions> WithoutFinding(string originKey, string findingId) =>
    [
        .. Decisions.Findings.Select(item => string.Equals(item.OriginKey, originKey, StringComparison.Ordinal)
            ? item with { Choices = [.. item.Choices.Where(choice => !string.Equals(choice.FindingId, findingId, StringComparison.Ordinal))] }
            : item),
    ];

    private (MatchGroup Group, VaultItem Merged, ReviewGroup Review) RequireGroup(string groupKey)
    {
        ArgumentNullException.ThrowIfNull(groupKey);
        if (!groupIndexes.TryGetValue(groupKey, out int index))
        {
            throw new ArgumentException($"No group has the key {groupKey}.", nameof(groupKey));
        }

        return (Result.Match.Groups[index], Result.Merged.Items[index], groups[index]);
    }

    private ReviewGroup RequireItem(string originKey)
    {
        ArgumentNullException.ThrowIfNull(originKey);
        return GroupOf(originKey) ?? throw new ArgumentException($"No group holds {originKey}.", nameof(originKey));
    }

    private ItemFindingsResult RequireFinding(string originKey, string findingId)
    {
        ArgumentNullException.ThrowIfNull(originKey);
        ArgumentNullException.ThrowIfNull(findingId);
        if (findingsByItem.TryGetValue(originKey, out ItemFindingsResult? item) && item.Findings.Any(finding => string.Equals(finding.Id, findingId, StringComparison.Ordinal)))
        {
            return item;
        }

        throw new ArgumentException($"{originKey} has no finding {findingId}.", nameof(findingId));
    }

    // The scanner finds secrets stored in the wrong field, such as a TOTP seed in a user name. Such a field stays masked
    // for as long as the member holds the finding, also after the finding is rejected and the secret stays there.
    private bool IsSecret(string fieldPath, string originKey) =>
        SecretPaths.Contains(fieldPath) || HasFinding(originKey, path => string.Equals(path, fieldPath, StringComparison.Ordinal));

    private bool HasFinding(string originKey, Func<string, bool> inField) =>
        findingsByItem.TryGetValue(originKey, out ItemFindingsResult? item)
        && item.Findings.Any(finding => inField(finding.Location.FieldPath));

    // Placement reports a problem as "<finding ID>: <what happened>". Returns what happened, or null when the finding was
    // placed without a problem.
    private string? ProblemOf(string originKey, string findingId)
    {
        if (!problemsByItem.TryGetValue(originKey, out List<string>? problems))
        {
            return null;
        }

        string prefix = findingId + ": ";
        string[] found = [.. problems.Where(problem => problem.StartsWith(prefix, StringComparison.Ordinal)).Select(problem => problem[prefix.Length..])];
        return found.Length == 0 ? null : string.Join("; ", found);
    }

    // Custom fields show as "name: value". Hidden values are masked, and every value is masked when a finding lies in a
    // custom field of the member.
    private Cell CustomFields(VaultItem member, string originKey)
    {
        if (member.Fields.Count == 0)
        {
            return new Cell(null, Secret: false);
        }

        bool findingInFields = HasFinding(originKey, path => SecretLocation.TryParseField(path, out _, out _));
        bool secret = findingInFields || member.Fields.Any(field => field.Type == CustomFieldType.Hidden);
        string shown = string.Join("; ", member.Fields.Select(field => Describe(field, mask: false)));
        string masked = string.Join("; ", member.Fields.Select(field => Describe(field, mask: findingInFields || field.Type == CustomFieldType.Hidden)));
        return new Cell(shown, secret, masked);
    }

    private static string Describe(CustomField field, bool mask) =>
        (field.Name ?? string.Empty) + ": " + (field.Type == CustomFieldType.Linked ? "linked" : ReviewText.Value(field.Value, mask));

    private ReviewField ToField(FieldContext context, RowData row)
    {
        bool revealed = revealedFields.Contains((context.Group.Key, row.FieldPath));
        return new ReviewField(
            row.FieldPath,
            row.Label,
            [.. row.Cells.Select(cell => Display(cell, revealed))],
            row.Cells.Any(cell => cell.Secret && !string.IsNullOrWhiteSpace(cell.Value)),
            revealed,
            context.Changed.Contains(row.FieldPath),
            context.Review.ConflictingFields.Contains(row.FieldPath, StringComparer.Ordinal),
            Decisions.Find(context.Group.Key, row.FieldPath),
            context.Report?.Fields.FirstOrDefault(field => string.Equals(field.FieldPath, row.FieldPath, StringComparison.Ordinal))?.Resolution,
            row.AcceptsMember && context.Decidable,
            row.AcceptsEmpty && context.Decidable);
    }

    private string? FindingText(string originKey, SecretFinding finding) =>
        sources.TryGetValue(originKey, out ScannedItem? item)
        && new PlacementState(item.Source.Item, item.AttachmentTexts).TryRead(finding.Location, out string? text)
            ? text
            : null;

    // A field value of one member: the raw value, whether it is secret, and how it shows while masked when that differs
    // from the plain mask.
    private sealed record Cell(string? Value, bool Secret, string? Masked = null)
    {
        private bool PrintMembers(System.Text.StringBuilder builder)
        {
            builder.Append("Secret = ").Append(Secret);
            return true;
        }
    }

    // One row before masking: the field, each member's value, and which decisions the field takes.
    private sealed record RowData(string FieldPath, string Label, Cell[] Cells, bool AcceptsMember, bool AcceptsEmpty)
    {
        private bool PrintMembers(System.Text.StringBuilder builder)
        {
            builder.Append("FieldPath = ").Append(FieldPath);
            return true;
        }
    }

    private sealed record FieldContext(MatchGroup Group, ReviewGroup Review, HashSet<string> Changed, GroupReport? Report, bool Decidable);
}
