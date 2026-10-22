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

    /// <summary>The password equals the user name.</summary>
    EqualsUsername = 3,

    /// <summary>The password equals the item name.</summary>
    EqualsItemName = 4,
}
