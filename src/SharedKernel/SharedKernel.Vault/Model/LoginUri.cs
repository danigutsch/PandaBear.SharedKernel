using System.Text;

namespace SharedKernel.Vault.Model;

/// <summary>Website address attached to a login.</summary>
/// <param name="Value">
/// Address as stored by the source vault. Not always a valid absolute URI: apps, bare hosts, and regular
/// expressions are allowed.
/// </param>
/// <param name="Match">Autofill match detection, or <see langword="null"/> for the vault default.</param>
public sealed record LoginUri(string Value, UriMatch? Match = null)
{
    /// <summary>Prints the match detection only, because an address can hold credentials or tokens in its user info or query.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Match = ").Append(Match);
        return true;
    }
}
