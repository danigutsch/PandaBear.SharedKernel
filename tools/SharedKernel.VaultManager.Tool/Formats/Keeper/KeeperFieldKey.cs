namespace PandaBear.VaultManager.Tool.Formats.Keeper;

/// <summary>Parses Keeper custom field keys such as <c>$oneTimeCode::1</c>, <c>$url:account:1</c>, or <c>$text:label</c>.</summary>
internal static class KeeperFieldKey
{
    /// <summary>Splits a key into field type and label.</summary>
    /// <param name="key">Key from <c>custom_fields</c> or a CSV pair.</param>
    /// <returns>Type without <c>$</c> (empty for plain labels) and label without a trailing <c>:&lt;digit&gt;</c>.</returns>
    public static (string Type, string Label) Parse(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        string type = "";
        string label = key;
        if (key.StartsWith('$'))
        {
            int colon = key.IndexOf(':', StringComparison.Ordinal);
            type = (colon > 0 ? key[1..colon] : key[1..]).Trim();
            label = colon > 0 ? key[(colon + 1)..].Trim() : "";
        }

        if (label.Length >= 2 && label[^2] == ':' && char.IsAsciiDigit(label[^1]))
        {
            label = label[..^2];
        }

        return (type, label);
    }
}
