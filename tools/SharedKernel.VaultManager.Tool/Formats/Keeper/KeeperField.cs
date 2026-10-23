using System.Text;
using System.Text.Json;

namespace PandaBear.VaultManager.Tool.Formats.Keeper;

/// <summary>Typed or custom field of a Keeper record.</summary>
/// <param name="Type">Keeper field type without the <c>$</c> prefix, or empty for a plain custom field.</param>
/// <param name="Label">Field label, or empty.</param>
/// <param name="Value">Field value: a string, number, boolean, object, or an array of those.</param>
internal sealed record KeeperField(string Type, string Label, JsonElement Value)
{
    /// <summary>Name used for a custom field: the label, else the type.</summary>
    public string DisplayName => Label.Length > 0 ? Label : Type;

    /// <summary>Values of the field: array items, or the single value.</summary>
    public IEnumerable<JsonElement> Values => Value.ValueKind == JsonValueKind.Array ? Value.EnumerateArray() : [Value];

    /// <summary>Prints only the type and label, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Type = ").Append(Type).Append(", Label = ").Append(Label);
        return true;
    }
}
