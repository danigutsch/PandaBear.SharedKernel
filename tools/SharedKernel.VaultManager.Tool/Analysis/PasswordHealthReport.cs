using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Analysis;

/// <summary>Password weaknesses of a vault. Holds item names and issue kinds only, never passwords or hashes.</summary>
/// <param name="Issues">Issues in item order.</param>
internal sealed record PasswordHealthReport(IReadOnlyList<PasswordIssue> Issues)
{
    /// <summary>Minimum password length that is not reported as too short.</summary>
    public const int MinimumLength = 12;

    /// <summary>
    /// Reports reuse of one password on items with different normalized hosts, passwords shorter than
    /// <see cref="MinimumLength"/>, passwords of a single character class, and easily guessed passwords: those equal to the
    /// user name or item name, reported once per item without saying which. Reuse is found by grouping passwords in
    /// memory; nothing is persisted.
    /// </summary>
    /// <param name="export">Vault to analyze.</param>
    /// <returns>The report.</returns>
    public static PasswordHealthReport Analyze(VaultExport export)
    {
        ArgumentNullException.ThrowIfNull(export);

        var withPassword = export.Items
            .Where(item => !ItemNormalizer.IsAbsent(item.Login?.Password))
            .Select(item => (Item: item, Password: item.Login!.Password!, Hosts: HostKey(item)))
            .ToArray();
        var byPassword = withPassword.ToLookup(entry => entry.Password, StringComparer.Ordinal);

        var issues = new List<PasswordIssue>();
        foreach (var (item, password, hosts) in withPassword)
        {
            string[] reusedOn = [.. byPassword[password].Where(other => !string.Equals(other.Hosts, hosts, StringComparison.Ordinal)).Select(other => other.Item.Name)];
            if (reusedOn.Length > 0)
            {
                issues.Add(new PasswordIssue(item.Name, PasswordIssueKind.Reused, reusedOn));
            }

            if (password.Length < MinimumLength)
            {
                issues.Add(new PasswordIssue(item.Name, PasswordIssueKind.TooShort, []));
            }

            if (CharacterClasses(password) == 1)
            {
                issues.Add(new PasswordIssue(item.Name, PasswordIssueKind.SingleCharacterClass, []));
            }

            string trimmed = password.Trim();
            if (string.Equals(trimmed, item.Login!.Username?.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, item.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new PasswordIssue(item.Name, PasswordIssueKind.EasilyGuessed, []));
            }
        }

        return new PasswordHealthReport(issues);
    }

    private static string HostKey(VaultItem item) => string.Join(
        '\n',
        (item.Login?.Uris ?? []).Select(uri => ItemNormalizer.Host(uri.Value)).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    private static int CharacterClasses(string password)
    {
        bool lower = false, upper = false, digit = false, other = false;
        foreach (char character in password)
        {
            if (char.IsLower(character))
            {
                lower = true;
            }
            else if (char.IsUpper(character))
            {
                upper = true;
            }
            else if (char.IsDigit(character))
            {
                digit = true;
            }
            else
            {
                other = true;
            }
        }

        return (lower ? 1 : 0) + (upper ? 1 : 0) + (digit ? 1 : 0) + (other ? 1 : 0);
    }
}
