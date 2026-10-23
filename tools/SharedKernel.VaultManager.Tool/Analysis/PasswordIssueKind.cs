namespace PandaBear.VaultManager.Tool.Analysis;

/// <summary>Password weaknesses.</summary>
internal enum PasswordIssueKind
{
    /// <summary>The same password is used on items with different hosts.</summary>
    Reused = 0,

    /// <summary>
    /// The password is shorter than <see cref="PasswordHealthReport.MinimumLength"/> characters, uses only one of
    /// lowercase, uppercase, digits, or symbols, or equals the item's user name or name, ignoring case and surrounding
    /// whitespace. The issue does not say which check failed, because a reason disclosed next to the item's names lets a
    /// reader infer the password.
    /// </summary>
    Weak = 1,
}
