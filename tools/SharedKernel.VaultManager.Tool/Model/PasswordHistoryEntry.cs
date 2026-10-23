using System.Text;

namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Previously used password.</summary>
/// <param name="Password">Former password.</param>
/// <param name="LastUsedDate">When the password was replaced.</param>
internal sealed record PasswordHistoryEntry(string Password, DateTimeOffset LastUsedDate)
{
    /// <summary>Prints only non-secret members, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("LastUsedDate = ").Append(LastUsedDate.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        return true;
    }
}
