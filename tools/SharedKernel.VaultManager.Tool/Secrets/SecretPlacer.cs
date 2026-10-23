using System.Globalization;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>
/// Moves accepted findings to their targets. Values are re-read from the item at their recorded location; source text
/// is removed only after its placement succeeded, and values over a Bitwarden field budget stay where they are.
/// </summary>
internal static class SecretPlacer
{
    /// <summary>Places accepted findings.</summary>
    /// <param name="item">Source item.</param>
    /// <param name="accepted">Findings the user accepted, including pre-accepted ones.</param>
    /// <param name="attachmentTexts">Attachment text by <c>attachment:</c> field path, for findings from attachments.</param>
    /// <returns>The updated item, new items, and problems described without values.</returns>
    public static PlacementResult Apply(VaultItem item, IEnumerable<SecretFinding> accepted, IReadOnlyDictionary<string, string>? attachmentTexts = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(accepted);
        List<SecretFinding> findings = [.. accepted.OrderBy(finding => finding.Location.FieldPath, StringComparer.Ordinal).ThenBy(finding => finding.Location.Start)];
        if (findings.Count == 0)
        {
            return new PlacementResult { Item = item };
        }

        var state = new PlacementState(item, attachmentTexts);
        foreach (SecretFinding finding in findings)
        {
            if (!state.TryRead(finding.Location, out string? value))
            {
                state.Problems.Add($"{finding.Id}: source text changed or is missing; not placed");
                continue;
            }

            if (Place(state, finding, value!) && !finding.Location.FieldPath.StartsWith(SecretLocation.AttachmentPrefix, StringComparison.Ordinal))
            {
                state.Removals.Add(finding.Location);
            }
        }

        return state.Build();
    }

    private static bool Place(PlacementState state, SecretFinding finding, string value)
    {
        PlacementTarget target = finding.ProposedTarget;
        if (target.Kind == PlacementTargetKind.NotMoved)
        {
            state.Problems.Add($"{finding.Id}: not moved ({target.Reason})");
            return false;
        }

        if (finding.Kind == SecretKind.Totp)
        {
            return PlaceTotp(state, finding, value, target.Kind == PlacementTargetKind.LoginTotp);
        }

        switch (target.Kind)
        {
            case PlacementTargetKind.LoginPassword:
                return state.IsLogin && string.IsNullOrEmpty(state.Password)
                    ? FitsOrReport(state, finding, value, BitwardenLimits.ValueEncryptedLength) && Assign(() => state.Password = value)
                    : AddField(state, finding, "Password (notes)", value, CustomFieldType.Hidden);
            case PlacementTargetKind.LoginUsername:
                return state.IsLogin && string.IsNullOrEmpty(state.Username)
                    ? FitsOrReport(state, finding, value, BitwardenLimits.ValueEncryptedLength) && Assign(() => state.Username = value)
                    : AddField(state, finding, "Username (notes)", value, CustomFieldType.Text);
            case PlacementTargetKind.LoginUris:
                return state.IsLogin ? Assign(() => state.AddUri(value)) : AddField(state, finding, "URI", value, CustomFieldType.Text);
            case PlacementTargetKind.LoginFromConnection:
                return PlaceConnection(state, finding, value);
            case PlacementTargetKind.HiddenField:
            case PlacementTargetKind.TextField:
                string name = target.NameFromLabel && finding.LabelLocation is { } label && state.TryRead(label, out string? labelText)
                    && !string.IsNullOrWhiteSpace(labelText)
                    ? labelText!.Trim()
                    : target.FieldName ?? finding.Kind.ToString();
                return AddField(state, finding, name, value, target.Kind == PlacementTargetKind.HiddenField ? CustomFieldType.Hidden : CustomFieldType.Text);
            case PlacementTargetKind.NewSshKeyItem:
                return PlaceSshKey(state, finding, value);
            case PlacementTargetKind.NewCardItem:
                return PlaceCard(state, finding, value);
            default:
                state.Problems.Add($"{finding.Id}: unsupported target");
                return false;
        }
    }

    private static bool PlaceTotp(PlacementState state, SecretFinding finding, string value, bool preferLogin)
    {
        if (finding.RuleId == "otpauth-migration")
        {
            bool all = true;
            foreach (MigratedOtp entry in TotpUri.DecodeMigration(value))
            {
                if (!entry.IsSupported || entry.Uri is null)
                {
                    state.Problems.Add($"{finding.Id}: migration entry not placed ({entry.Problem})");
                    all = false;
                    continue;
                }

                all &= StoreTotp(state, finding, entry.Uri, preferLogin);
            }

            return all;
        }

        string? uri = null;
        bool normalized = finding.RuleId == SecretScanner.MaFileSharedSecretRule
            ? SecretText.TryDecodeBase64(value, out byte[]? shared) && shared is not null && TotpUri.TryNormalize("steam://" + Base32.Encode(shared), null, null, out uri)
            : TotpUri.TryNormalize(value, state.Source.Name, state.Username, out uri);
        if (!normalized || uri is null)
        {
            state.Problems.Add($"{finding.Id}: not a usable TOTP secret; not placed");
            return false;
        }

        return StoreTotp(state, finding, uri, preferLogin);
    }

    private static bool StoreTotp(PlacementState state, SecretFinding finding, string uri, bool preferLogin)
    {
        if (!FitsOrReport(state, finding, uri, BitwardenLimits.ShortEncryptedLength))
        {
            return false;
        }

        if (state.Totp == uri || state.Fields.Any(field => field.Value == uri))
        {
            return true;
        }

        if (state.IsLogin && preferLogin && string.IsNullOrEmpty(state.Totp))
        {
            state.Totp = uri;
            return true;
        }

        int number = 2;
        while (state.HasFieldName(string.Create(CultureInfo.InvariantCulture, $"TOTP ({number})")))
        {
            number++;
        }

        state.Fields.Add(new CustomField(string.Create(CultureInfo.InvariantCulture, $"TOTP ({number})"), uri, CustomFieldType.Hidden));
        return true;
    }

    private static bool PlaceConnection(PlacementState state, SecretFinding finding, string value)
    {
        if (!FitsOrReport(state, finding, value, BitwardenLimits.ValueEncryptedLength))
        {
            return false;
        }

        bool parsed = ConnectionParts.TryParseUri(value, out ConnectionParts? parts) || ConnectionParts.TryParseConnectionString(value, out parts);
        if (parsed && parts is not null && state.IsLogin)
        {
            if (string.IsNullOrEmpty(state.Username) && parts.Username is not null)
            {
                state.Username = parts.Username;
            }

            if (string.IsNullOrEmpty(state.Password) && parts.Password is not null)
            {
                state.Password = parts.Password;
            }

            if (parts.Uri is not null && value.Contains("://", StringComparison.Ordinal))
            {
                state.AddUri(parts.Uri);
            }
        }

        return AddField(state, finding, finding.ProposedTarget.FieldName ?? "Connection string", value, CustomFieldType.Hidden);
    }

    private static bool PlaceSshKey(PlacementState state, SecretFinding finding, string value)
    {
        state.TryReadWhole(finding.Location.FieldPath, out string? source);
        string? supplied = source?.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.StartsWith("ssh-ed25519 ", StringComparison.Ordinal));
        SshKeyConversion conversion = SshKeyConverter.Convert(value, supplied);
        if (conversion.Key is null)
        {
            state.Problems.Add($"{finding.Id}: SSH key not converted ({conversion.Problem}); kept as a hidden field when it fits");
            return AddField(state, finding, "Private key", value, CustomFieldType.Hidden);
        }

        state.AddNewItem(
            new VaultItem
            {
                Type = VaultItemType.SshKey,
                Name = state.Source.Name + " SSH key",
                FolderPath = state.Source.FolderPath,
                SshKey = conversion.Key,
                Origin = state.Source.Origin,
            },
            finding.Id);
        return true;
    }

    private static bool PlaceCard(PlacementState state, SecretFinding finding, string value)
    {
        string digits = new([.. value.Where(char.IsAsciiDigit)]);
        if (!CardNumberValidator.TryGetBrand(digits, out string? brand))
        {
            state.Problems.Add($"{finding.Id}: not a valid card number; not placed");
            return false;
        }

        state.AddNewItem(
            new VaultItem
            {
                Type = VaultItemType.Card,
                Name = state.Source.Name,
                FolderPath = state.Source.FolderPath,
                Card = new CardData { Number = digits, Brand = brand },
                Origin = state.Source.Origin,
            },
            finding.Id);
        return true;
    }

    private static bool AddField(PlacementState state, SecretFinding finding, string name, string value, CustomFieldType type)
    {
        if (!FitsOrReport(state, finding, value, BitwardenLimits.ValueEncryptedLength) || !FitsOrReport(state, finding, name, BitwardenLimits.ShortEncryptedLength))
        {
            return false;
        }

        int replaced = -1;
        if (SecretLocation.TryParseField(finding.Location.FieldPath, out int index, out bool isName) && !isName
            && index < state.Source.Fields.Count && (state.Source.Fields[index].Value ?? string.Empty).Trim().Length == finding.Location.Length)
        {
            replaced = index;
        }

        string unique = name;
        for (int number = 2; state.HasFieldName(unique, replaced); number++)
        {
            unique = string.Create(CultureInfo.InvariantCulture, $"{name} ({number})");
        }

        state.Fields.Add(new CustomField(unique, value, type));
        return true;
    }

    private static bool FitsOrReport(PlacementState state, SecretFinding finding, string value, int limit)
    {
        if (BitwardenLimits.Fits(value, limit))
        {
            return true;
        }

        state.Problems.Add($"{finding.Id}: value exceeds the Bitwarden field budget; left in place");
        return false;
    }

    private static bool Assign(Action action)
    {
        action();
        return true;
    }
}
