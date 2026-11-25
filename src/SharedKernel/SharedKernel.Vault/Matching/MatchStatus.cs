namespace SharedKernel.Vault.Matching;

/// <summary>How the members of a match group relate.</summary>
public enum MatchStatus
{
    /// <summary>The group has one member.</summary>
    Unique = 0,

    /// <summary>Every member holds the same values after normalization.</summary>
    Identical = 1,

    /// <summary>Members differ in at least one field.</summary>
    Different = 2,
}
