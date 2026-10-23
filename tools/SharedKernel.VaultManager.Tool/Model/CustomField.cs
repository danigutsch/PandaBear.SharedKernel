using System.Text;

namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Named custom value on a vault item.</summary>
/// <param name="Name">Field label.</param>
/// <param name="Value">Field value.</param>
/// <param name="Type">Field kind.</param>
/// <param name="LinkedId">Linked property identifier for <see cref="CustomFieldType.Linked"/> fields.</param>
internal sealed record CustomField(string? Name, string? Value, CustomFieldType Type, int? LinkedId = null)
{
    /// <summary>Prints only non-secret members, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Name = ").Append(Name).Append(", Type = ").Append(Type);
        return true;
    }
}
