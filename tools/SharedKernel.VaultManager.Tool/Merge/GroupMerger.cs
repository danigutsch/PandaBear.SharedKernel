using System.Globalization;
using System.Text;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>Merges the members of one group field by field. Create one instance per group.</summary>
internal sealed class GroupMerger
{
    private readonly MatchGroup group;
    private readonly MergePolicy policy;
    private readonly MergeDecisions decisions;
    private readonly DateTimeOffset now;
    private readonly VaultItem[] newestFirst;
    private readonly VaultItem primary;
    private readonly HashSet<string> changed;
    private readonly List<CustomField> preservedFields = [];
    private readonly List<PasswordHistoryEntry> preservedHistory = [];
    private readonly List<(string Source, string Notes)> appendedNotes = [];
    private readonly List<string> conversionNotes = [];

    /// <summary>Prepares a group merge.</summary>
    /// <param name="group">Group with at least two members.</param>
    /// <param name="policy">Conflict policy.</param>
    /// <param name="decisions">User decisions.</param>
    /// <param name="now">Current time, used for preserved passwords when no member has a timestamp.</param>
    public GroupMerger(MatchGroup group, MergePolicy policy, MergeDecisions decisions, DateTimeOffset now)
    {
        this.group = group;
        this.policy = policy;
        this.decisions = decisions;
        this.now = now;
        newestFirst = [.. group.Members.OrderByDescending(member => member.LastModified.HasValue).ThenByDescending(member => member.LastModified)];
        primary = newestFirst[0];
        changed = [.. FieldDiff.ChangedPaths(group.Members)];
    }

    /// <summary>Fields that need a user decision.</summary>
    public List<UnresolvedConflict> Unresolved { get; } = [];

    /// <summary>Changed fields and how each was resolved.</summary>
    public List<FieldResolution> Resolutions { get; } = [];

    /// <summary>Conflicts resolved by the policy without a decision.</summary>
    public int AutoResolved
    {
        get; private set;
    }

    /// <summary>Merges the group.</summary>
    /// <returns>The merged item.</returns>
    public VaultItem Merge()
    {
        var type = primary.Type;
        var result = new VaultItem
        {
            Type = type,
            Name = primary.Name,
            Origin = primary.Origin,
            Login = type == VaultItemType.Login ? new LoginData() : null,
            Card = type == VaultItemType.Card ? new CardData() : null,
            Identity = type == VaultItemType.Identity ? new IdentityData() : null,
            SshKey = type == VaultItemType.SshKey ? new SshKeyData() : null,
            Favorite = group.Members.Any(member => member.Favorite),
            Reprompt = group.Members.Any(member => member.Reprompt),
            CreationDate = group.Members.Min(member => member.CreationDate),
            RevisionDate = group.Members.Max(member => member.RevisionDate),
            Attachments = [.. GroupAttachments.Choose(newestFirst).Select(file => file.Attachment)],
        };

        MergeType();
        result = MergeName(result);
        result = MergeFolder(result);
        Record(FieldPaths.Favorite, ResolutionKind.OneSided);
        Record(FieldPaths.Reprompt, ResolutionKind.OneSided);

        foreach (var field in ScalarFields.All)
        {
            result = field.AppliesTo(type) ? MergeScalar(result, field) : PreserveForeignScalar(result, field);
        }

        result = MergeLoginCollections(result);
        result = AppendNotes(result);

        IReadOnlyList<CustomField> fields = Choose(FieldPaths.Fields) is { Kind: FieldChoiceKind.Empty }
            ? []
            : [.. newestFirst.SelectMany(member => member.Fields)];
        RecordUnion(FieldPaths.Fields);
        IReadOnlyList<PasswordHistoryEntry> history = Choose(FieldPaths.PasswordHistory) is { Kind: FieldChoiceKind.Empty }
            ? []
            : [.. newestFirst.SelectMany(member => member.PasswordHistory)];
        RecordUnion(FieldPaths.PasswordHistory);
        Resolutions.Sort((left, right) => FieldDiff.ReportOrder(left.FieldPath).CompareTo(FieldDiff.ReportOrder(right.FieldPath)));

        return result with
        {
            Fields = FieldDiff.DistinctFields(fields.Concat(preservedFields)),
            PasswordHistory =
            [
                .. history.Concat(preservedHistory)
                    .GroupBy(entry => entry.Password, StringComparer.Ordinal)
                    .Select(entries => entries.MaxBy(entry => entry.LastUsedDate)!)
                    .OrderByDescending(entry => entry.LastUsedDate),
            ],
            ConversionNotes = [.. group.Members.SelectMany(member => member.ConversionNotes).Concat(conversionNotes).Distinct(StringComparer.Ordinal)],
        };
    }

    private static string Source(VaultItem member) => Path.GetFileName(member.Origin.SourceFile);

    private static string PreservedName(string label, VaultItem member) => label + " (from " + Source(member) + ")";

    private void MergeType()
    {
        foreach (var member in newestFirst.Where(member => member.Type != primary.Type).DistinctBy(member => member.Type))
        {
            conversionNotes.Add(string.Create(CultureInfo.InvariantCulture, $"Type {member.Type} from {Source(member)} was not used; the item is a {primary.Type}."));
        }

        Record(FieldPaths.Type, ResolutionKind.Newest);
    }

    private VaultItem MergeName(VaultItem result)
    {
        var winner = ChosenMember(FieldPaths.Name) ?? primary;
        string winnerKey = ItemNormalizer.Title(winner.Name);
        var seen = new HashSet<string>(StringComparer.Ordinal) { winnerKey };
        foreach (var member in newestFirst)
        {
            if (seen.Add(ItemNormalizer.Title(member.Name)))
            {
                preservedFields.Add(new CustomField(PreservedName("Name", member), member.Name, CustomFieldType.Text));
            }
        }

        Record(FieldPaths.Name, winner == primary ? ResolutionKind.Newest : ResolutionKind.Decision);
        return result with
        {
            Name = winner.Name
        };
    }

    private VaultItem MergeFolder(VaultItem result)
    {
        // The folder is a single value: a folder on one side is kept, and different folders go through the policy.
        var values = new List<(string Key, List<VaultItem> Holders)>();
        foreach (var member in newestFirst.Where(member => !string.IsNullOrWhiteSpace(member.FolderPath)))
        {
            int existing = values.FindIndex(value => string.Equals(value.Key, member.FolderPath, StringComparison.Ordinal));
            if (existing < 0)
            {
                values.Add((member.FolderPath!, [member]));
            }
            else
            {
                values[existing].Holders.Add(member);
            }
        }

        VaultItem winner;
        ResolutionKind resolution;
        if (ChosenMember(FieldPaths.Folder) is { } chosen)
        {
            winner = chosen;
            resolution = ResolutionKind.Decision;
        }
        else if (values.Count == 0)
        {
            return result with
            {
                FolderPath = null
            };
        }
        else if (values.Count == 1)
        {
            winner = values[0].Holders[0];
            resolution = values[0].Holders.Count == group.Members.Count ? ResolutionKind.Equal : ResolutionKind.OneSided;
        }
        else
        {
            (winner, resolution) = PickWinner(values);
            if (resolution == ResolutionKind.Unresolved)
            {
                Unresolved.Add(new UnresolvedConflict(group.Key, FieldPaths.Folder, group.MemberKeys, [.. group.Members.Select(member => member.Name)]));
            }
            else
            {
                AutoResolved++;
            }
        }

        foreach (var (key, holders) in values)
        {
            if (!string.Equals(key, winner.FolderPath, StringComparison.Ordinal))
            {
                string kept = winner.FolderPath is null ? "no folder" : "folder \"" + winner.FolderPath + "\"";
                conversionNotes.Add($"Folder \"{key}\" from {Source(holders[0])} was not used; the item is in {kept}.");
            }
        }

        Record(FieldPaths.Folder, resolution, always: resolution == ResolutionKind.Decision);
        return result with
        {
            FolderPath = winner.FolderPath
        };
    }

    private VaultItem MergeScalar(VaultItem result, ScalarField field)
    {
        // Distinct present values, newest holder first. Each entry keeps every member holding that value.
        var values = new List<(string Key, List<VaultItem> Holders)>();
        foreach (var member in newestFirst)
        {
            string key = FieldDiff.ScalarKey(field, member);
            if (key.Length == 0)
            {
                continue;
            }

            int existing = values.FindIndex(value => string.Equals(value.Key, key, StringComparison.Ordinal));
            if (existing < 0)
            {
                values.Add((key, [member]));
            }
            else
            {
                values[existing].Holders.Add(member);
            }
        }

        var choice = Choose(field.Path);
        if (choice is { Kind: FieldChoiceKind.Empty })
        {
            Record(field.Path, ResolutionKind.Decision, always: values.Count > 0);
            return field.Set(result, null);
        }

        VaultItem? winner;
        ResolutionKind resolution;
        if (ChosenMember(field.Path) is { } chosen)
        {
            winner = chosen;
            resolution = ResolutionKind.Decision;
        }
        else if (values.Count == 0)
        {
            return field.Set(result, null);
        }
        else if (values.Count == 1)
        {
            winner = values[0].Holders[0];
            resolution = values[0].Holders.Count == group.Members.Count ? ResolutionKind.Equal : ResolutionKind.OneSided;
        }
        else
        {
            (winner, resolution) = PickWinner(values);
            if (resolution == ResolutionKind.Unresolved)
            {
                Unresolved.Add(new UnresolvedConflict(group.Key, field.Path, group.MemberKeys, [.. group.Members.Select(member => member.Name)]));
            }
            else
            {
                AutoResolved++;
            }
        }

        Record(field.Path, resolution, always: resolution == ResolutionKind.Decision);
        string winnerKey = FieldDiff.ScalarKey(field, winner);
        foreach (var (key, holders) in values)
        {
            if (!string.Equals(key, winnerKey, StringComparison.Ordinal))
            {
                Preserve(field, holders[0], field.Get(holders[0])!, winner);
            }
        }

        string? winnerValue = winnerKey.Length == 0 ? null : field.Get(winner);
        return field.Set(result, winnerValue);
    }

    private (VaultItem Winner, ResolutionKind Resolution) PickWinner(List<(string Key, List<VaultItem> Holders)> values)
    {
        // values[0] holds the newest present value, or the first member's value when no holder has a timestamp.
        var provisional = values[0].Holders[0];
        if (policy.Kind == MergePolicyKind.Ask)
        {
            return (provisional, ResolutionKind.Unresolved);
        }

        if (policy.Kind == MergePolicyKind.PreferSource && policy.PreferredSourceFile is { } preferred)
        {
            foreach (var (_, holders) in values)
            {
                if (holders.FirstOrDefault(holder => string.Equals(holder.Origin.SourceFile, preferred, StringComparison.Ordinal)) is { } match)
                {
                    return (match, ResolutionKind.PreferredSource);
                }
            }
        }

        bool everyHolderTimestamped = values.All(value => value.Holders.All(holder => holder.LastModified.HasValue));
        bool tie = values[0].Holders[0].LastModified == values[1].Holders[0].LastModified;
        return everyHolderTimestamped && !tie ? (provisional, ResolutionKind.Newest) : (provisional, ResolutionKind.Unresolved);
    }

    private void Preserve(ScalarField field, VaultItem holder, string value, VaultItem winner)
    {
        if (field.Path == FieldPaths.LoginPassword)
        {
            preservedHistory.Add(new PasswordHistoryEntry(value, holder.LastModified ?? winner.LastModified ?? now));
        }
        else if (field.Path == FieldPaths.Notes && !BitwardenLimits.Fits(value, BitwardenLimits.ValueEncryptedLength))
        {
            appendedNotes.Add((Source(holder), value));
        }
        else
        {
            preservedFields.Add(new CustomField(PreservedName(field.Label, holder), value, CustomFieldType.Hidden));
        }
    }

    private VaultItem PreserveForeignScalar(VaultItem result, ScalarField field)
    {
        // The field belongs to another item type: keep every distinct value as a hidden field.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in newestFirst)
        {
            string key = FieldDiff.ScalarKey(field, member);
            if (key.Length > 0 && seen.Add(key))
            {
                preservedFields.Add(new CustomField(PreservedName(field.Label, member), field.Get(member), CustomFieldType.Hidden));
            }
        }

        return result;
    }

    private VaultItem MergeLoginCollections(VaultItem result)
    {
        bool dropUris = Choose(FieldPaths.LoginUris) is { Kind: FieldChoiceKind.Empty };
        bool dropPasskeys = Choose(FieldPaths.LoginFido2Credentials) is { Kind: FieldChoiceKind.Empty };
        LoginUri[] uris = dropUris ? [] : [.. newestFirst.SelectMany(member => member.Login?.Uris ?? []).DistinctBy(FieldDiff.UriKey, StringComparer.Ordinal)];
        Fido2Credential[] passkeys = dropPasskeys ? [] : [.. newestFirst.SelectMany(member => member.Login?.Fido2Credentials ?? []).DistinctBy(FieldDiff.PasskeyKey, StringComparer.Ordinal)];
        RecordUnion(FieldPaths.LoginUris);
        RecordUnion(FieldPaths.LoginFido2Credentials);

        if (result.Login is { } login)
        {
            return result with
            {
                Login = login with
                {
                    Uris = uris,
                    Fido2Credentials = passkeys
                }
            };
        }

        foreach (var uri in uris)
        {
            var holder = newestFirst.First(member => member.Login?.Uris.Contains(uri) == true);
            preservedFields.Add(new CustomField(PreservedName("URI", holder), uri.Value, CustomFieldType.Text));
        }

        foreach (var passkey in passkeys)
        {
            var holder = newestFirst.First(member => member.Login?.Fido2Credentials.Contains(passkey) == true);
            preservedFields.Add(new CustomField(PreservedName("Passkey private key", holder), passkey.KeyValue, CustomFieldType.Hidden));
            conversionNotes.Add($"Passkey for {passkey.RpId} from {Source(holder)} cannot be stored on a {result.Type}; its private key was kept as a hidden field.");
        }

        return result;
    }

    private VaultItem AppendNotes(VaultItem result)
    {
        if (appendedNotes.Count == 0)
        {
            return result;
        }

        var notes = new StringBuilder(result.Notes);
        foreach (var (source, text) in appendedNotes)
        {
            if (notes.Length > 0)
            {
                notes.Append("\n\n");
            }

            notes.Append("--- Notes from ").Append(source).Append(" ---\n").Append(text);
        }

        return result with
        {
            Notes = notes.ToString()
        };
    }

    private FieldChoice? Choose(string path) => decisions.Find(group.Key, path);

    private VaultItem? ChosenMember(string path)
    {
        if (Choose(path) is not { Kind: FieldChoiceKind.Member, OriginKey: { } originKey })
        {
            return null;
        }

        for (int index = 0; index < group.MemberKeys.Count; index++)
        {
            if (string.Equals(group.MemberKeys[index], originKey, StringComparison.Ordinal))
            {
                return group.Members[index];
            }
        }

        // A decision naming a member that is no longer in the group is stale and ignored.
        return null;
    }

    private void RecordUnion(string path)
    {
        bool decided = Choose(path) is { Kind: FieldChoiceKind.Empty };
        Record(path, decided ? ResolutionKind.Decision : ResolutionKind.Union, always: decided);
    }

    private void Record(string path, ResolutionKind resolution, bool always = false)
    {
        if (always || changed.Contains(path))
        {
            Resolutions.Add(new FieldResolution(path, resolution));
        }
    }
}
