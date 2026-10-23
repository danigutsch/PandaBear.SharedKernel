using PandaBear.VaultManager.Tool.Matching;
using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Analysis;

/// <summary>Password weaknesses of a vault. Holds item names, issue kinds, and counts only, never passwords or hashes.</summary>
/// <param name="Issues">Issues in item order.</param>
internal sealed record PasswordHealthReport(IReadOnlyList<PasswordIssue> Issues)
{
    /// <summary>Minimum password length that is not reported as weak.</summary>
    public const int MinimumLength = 12;

    /// <summary>
    /// Most names of other items that a <see cref="PasswordIssueKind.Reused"/> issue lists. The issue counts every other
    /// item, so the report stays proportional to the number of items when thousands of them share one password.
    /// </summary>
    public const int MaxRelatedItemNames = 3;

    /// <summary>
    /// Reports reuse of one password on items with different normalized hosts, and weak passwords: those shorter than
    /// <see cref="MinimumLength"/>, of a single character class, or equal to the user name or item name. A weak password
    /// is reported once per item without saying which check failed. Reuse is found by grouping the items once per password
    /// in memory; nothing is persisted.
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
        PasswordIssue?[] reused = Reuse(withPassword);

        var issues = new List<PasswordIssue>();
        for (int index = 0; index < withPassword.Length; index++)
        {
            (VaultItem item, string password, _) = withPassword[index];
            if (reused[index] is { } reuse)
            {
                issues.Add(reuse);
            }

            if (IsWeak(item, password))
            {
                issues.Add(new PasswordIssue(item.Name, PasswordIssueKind.Weak, [], 0));
            }
        }

        return new PasswordHealthReport(issues);
    }

    // Groups the items once per password. An item shares its password with the items of its group on other hosts: their
    // number is the group size minus the items on its own hosts, and their first names are found once per hosts key and
    // shared by the items with that key. The work therefore grows with the number of items, not with its square.
    private static PasswordIssue?[] Reuse((VaultItem Item, string Password, string Hosts)[] entries)
    {
        var issues = new PasswordIssue?[entries.Length];
        foreach (IGrouping<string, int> group in Enumerable.Range(0, entries.Length).GroupBy(index => entries[index].Password, StringComparer.Ordinal))
        {
            int[] members = [.. group];
            Dictionary<string, int> perHosts = members.CountBy(index => entries[index].Hosts, StringComparer.Ordinal).ToDictionary(StringComparer.Ordinal);
            if (perHosts.Count < 2)
            {
                continue;
            }

            var namesByHosts = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (int index in members)
            {
                string hosts = entries[index].Hosts;
                if (!namesByHosts.TryGetValue(hosts, out string[]? names))
                {
                    names =
                    [
                        .. members
                            .Where(other => !string.Equals(entries[other].Hosts, hosts, StringComparison.Ordinal))
                            .Take(MaxRelatedItemNames)
                            .Select(other => entries[other].Item.Name),
                    ];
                    namesByHosts.Add(hosts, names);
                }

                issues[index] = new PasswordIssue(entries[index].Item.Name, PasswordIssueKind.Reused, names, members.Length - perHosts[hosts]);
            }
        }

        return issues;
    }

    private static bool IsWeak(VaultItem item, string password)
    {
        string trimmed = password.Trim();
        return password.Length < MinimumLength
            || CharacterClasses(password) == 1
            || string.Equals(trimmed, item.Login!.Username?.Trim(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, item.Name.Trim(), StringComparison.OrdinalIgnoreCase);
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
