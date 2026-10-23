namespace PandaBear.VaultManager.Tool.Analysis;

/// <summary>Password weaknesses.</summary>
internal enum PasswordIssueKind
{
    /// <summary>The same password is used on items with different hosts.</summary>
    Reused = 0,

    /// <summary>The password is shorter than 12 characters.</summary>
    TooShort = 1,

    /// <summary>The password uses only one of lowercase, uppercase, digits, or symbols.</summary>
    SingleCharacterClass = 2,

    /// <summary>
    /// The password equals the item's user name or name, ignoring case and surrounding whitespace. The issue does not say
    /// which one, because a report that names the field also discloses the password.
    /// </summary>
    EasilyGuessed = 3,
}
