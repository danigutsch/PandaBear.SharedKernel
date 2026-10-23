using System.Text.RegularExpressions;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Patterns of rules that tests define for themselves.</summary>
internal static partial class TestPatterns
{
    /// <summary>Matches <c>tok_</c> followed by 16 lowercase letters or digits, with a one-second timeout.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex("tok_[a-z0-9]{16}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Token();
}
