using System.Text.RegularExpressions;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Deliberately catastrophic pattern with a 1 ms timeout, used to prove timeouts become findings.</summary>
internal static partial class SlowPatterns
{
    [GeneratedRegex("(a|aa|aaa)+(?<=(a|aa)+)\\1b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1)]
    public static partial Regex Catastrophic();
}
