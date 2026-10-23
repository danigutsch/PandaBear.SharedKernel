using System.Globalization;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Working copy of an item while findings are placed, plus the source removals to apply afterwards.</summary>
/// <param name="source">Source item.</param>
/// <param name="attachmentTexts">Attachment text by field path.</param>
internal sealed class PlacementState(VaultItem source, IReadOnlyDictionary<string, string>? attachmentTexts)
{
    private readonly List<LoginUri> uris = [.. source.Login?.Uris ?? []];
    private readonly List<(VaultItem Item, string FindingId)> newItems = [];
    private readonly HashSet<string> newItemNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> nextNumbers = new(StringComparer.Ordinal);

    /// <summary>Source item, unchanged.</summary>
    public VaultItem Source { get; } = source;

    /// <summary>Whether the item has login slots.</summary>
    public bool IsLogin { get; } = source.Type == VaultItemType.Login;

    /// <summary>Login user name being built.</summary>
    public string? Username { get; set; } = source.Login?.Username;

    /// <summary>Login password being built.</summary>
    public string? Password { get; set; } = source.Login?.Password;

    /// <summary>Login TOTP being built.</summary>
    public string? Totp { get; set; } = source.Login?.Totp;

    /// <summary>Custom fields added by placement.</summary>
    public List<CustomField> Fields { get; } = [];

    /// <summary>Problems, described without values.</summary>
    public List<string> Problems { get; } = [];

    /// <summary>Source locations to remove because their placement succeeded.</summary>
    public List<SecretLocation> Removals { get; } = [];

    /// <summary>
    /// Adds an item created by placing a finding. An item whose name an earlier new item of the same source already uses is
    /// renamed <c>name (n)</c> with the first free number, so items created together never match each other by title.
    /// </summary>
    /// <param name="item">New item.</param>
    /// <param name="findingId">Identifier of the finding that created it.</param>
    public void AddNewItem(VaultItem item, string findingId)
    {
        // One placement can create thousands of items, for example from an attachment, so the search for a free number
        // resumes where the previous one for the same name stopped.
        string name = item.Name;
        int number = nextNumbers.GetValueOrDefault(item.Name, 2);
        while (!newItemNames.Add(name))
        {
            name = string.Create(CultureInfo.InvariantCulture, $"{item.Name} ({number++})");
        }

        nextNumbers[item.Name] = number;
        newItems.Add((item with { Name = name }, findingId));
    }

    /// <summary>Adds a login URI unless an equal one exists.</summary>
    /// <param name="uri">URI.</param>
    public void AddUri(string uri)
    {
        if (!uris.Any(existing => string.Equals(existing.Value, uri, StringComparison.OrdinalIgnoreCase)))
        {
            uris.Add(new LoginUri(uri));
        }
    }

    /// <summary>Whether an original or added field already uses a name.</summary>
    /// <param name="name">Field name.</param>
    /// <param name="replacedIndex">Index of an original field the new field replaces, whose name stays available.</param>
    /// <returns><see langword="true"/> when taken.</returns>
    public bool HasFieldName(string name, int replacedIndex = -1)
    {
        return Fields.Any(field => field.Name == name) || Source.Fields.Where((_, index) => index != replacedIndex).Any(field => field.Name == name);
    }

    /// <summary>Reads the text at a location from the source item, checking it is still in range.</summary>
    /// <param name="location">Location.</param>
    /// <param name="value">Text at the location.</param>
    /// <returns><see langword="true"/> when the location is valid.</returns>
    public bool TryRead(SecretLocation location, out string? value)
    {
        value = null;
        if (!TryReadWhole(location.FieldPath, out string? text) || text is null
            || location.Start < 0 || location.Length <= 0 || location.End > text.Length)
        {
            return false;
        }

        value = text.Substring(location.Start, location.Length);
        return true;
    }

    /// <summary>Reads a whole field from the source item.</summary>
    /// <param name="fieldPath">Field path.</param>
    /// <param name="text">Field text.</param>
    /// <returns><see langword="true"/> when the field exists.</returns>
    public bool TryReadWhole(string fieldPath, out string? text)
    {
        text = fieldPath switch
        {
            SecretLocation.Notes => Source.Notes,
            SecretLocation.LoginPassword => Source.Login?.Password,
            SecretLocation.LoginUsername => Source.Login?.Username,
            SecretLocation.LoginTotp => Source.Login?.Totp,
            SecretLocation.CardNumber => Source.Card?.Number,
            _ when fieldPath.StartsWith(SecretLocation.AttachmentPrefix, StringComparison.Ordinal) => attachmentTexts?.GetValueOrDefault(fieldPath),
            _ when SecretLocation.TryParseField(fieldPath, out int index, out bool isName) && index < Source.Fields.Count =>
                isName ? Source.Fields[index].Name : Source.Fields[index].Value,
            _ => null,
        };
        return text is not null;
    }

    /// <summary>Builds the updated item with removals applied.</summary>
    /// <returns>The result.</returns>
    public PlacementResult Build()
    {
        string? notes = Source.Notes;
        string? password = Password;
        string? username = Username;
        string? totp = Totp;
        var keptFields = new List<CustomField>();
        foreach (var group in Removals.GroupBy(location => location.FieldPath, StringComparer.Ordinal))
        {
            switch (group.Key)
            {
                case SecretLocation.Notes:
                    notes = SourceTextEditor.Remove(notes ?? string.Empty, group);
                    break;
                case SecretLocation.LoginPassword when password == Source.Login?.Password:
                    password = SourceTextEditor.Remove(password ?? string.Empty, group);
                    break;
                case SecretLocation.LoginUsername when username == Source.Login?.Username:
                    username = SourceTextEditor.Remove(username ?? string.Empty, group);
                    break;
                case SecretLocation.LoginTotp when totp == Source.Login?.Totp:
                    totp = SourceTextEditor.Remove(totp ?? string.Empty, group);
                    break;
                default:
                    break;
            }
        }

        for (int index = 0; index < Source.Fields.Count; index++)
        {
            CustomField field = Source.Fields[index];
            string path = SecretLocation.FieldValue(index);
            var removals = Removals.Where(location => location.FieldPath == path).ToList();
            if (removals.Count == 0)
            {
                keptFields.Add(field);
                continue;
            }

            string? remaining = SourceTextEditor.Remove(field.Value ?? string.Empty, removals);
            if (remaining is not null)
            {
                keptFields.Add(field with { Value = remaining });
            }
        }

        keptFields.AddRange(Fields);
        LoginData? login = Source.Login is null && !IsLogin
            ? null
            : (Source.Login ?? new LoginData()) with { Username = username, Password = password, Totp = totp, Uris = [.. uris] };
        VaultItem item = Source with { Notes = notes, Fields = keptFields, Login = login };
        return new PlacementResult
        {
            Item = item,
            NewItems = [.. newItems.Select(created => created.Item)],
            NewItemFindingIds = [.. newItems.Select(created => created.FindingId)],
            Problems = [.. Problems],
        };
    }
}
