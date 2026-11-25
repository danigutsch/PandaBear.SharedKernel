using System.Text;

namespace SharedKernel.Vault.Catalog;

/// <summary>Text custom field of a catalog item.</summary>
/// <param name="Name">Field name.</param>
/// <param name="Value">Field value.</param>
public sealed record CatalogField(string Name, string Value)
{
    /// <summary>Prints only non-secret members, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Name = ").Append(Name);
        return true;
    }
}
