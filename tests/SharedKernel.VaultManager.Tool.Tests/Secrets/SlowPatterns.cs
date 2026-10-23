using System.Text.RegularExpressions;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Deliberately catastrophic pattern with a 1 ms timeout, used to prove timeouts become findings.</summary>
internal static partial class SlowPatterns
{
    /// <summary>
    /// Rule <c>slow-rule</c> whose keyword is <c>aaaa</c> and whose pattern is <see cref="Catastrophic"/>, so a match on a
    /// long run of <c>a</c> never completes within the timeout.
    /// </summary>
    public static SecretRule Rule() => new()
    {
        Id = "slow-rule",
        DisplayName = "Slow rule",
        Kind = SecretKind.ApiToken,
        Keywords = ["aaaa"],
        Pattern = Catastrophic(),
        Source = "test",
    };

    [GeneratedRegex("(a|aa|aaa)+(?<=(a|aa)+)\\1b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1)]
    public static partial Regex Catastrophic();
}
