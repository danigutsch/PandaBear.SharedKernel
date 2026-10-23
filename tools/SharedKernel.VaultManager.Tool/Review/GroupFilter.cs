namespace PandaBear.VaultManager.Tool.Review;

/// <summary>Which match groups the review lists.</summary>
internal enum GroupFilter
{
    /// <summary>Every group.</summary>
    All = 0,

    /// <summary>Groups with a field or a proposed finding that needs a decision before the merged vault can be written.</summary>
    Pending,

    /// <summary>Groups whose members hold different values for a field that a member choice decides.</summary>
    Conflict,

    /// <summary>Groups with an item that a weak match suggests joining with an item of another group.</summary>
    Suggested,

    /// <summary>Groups whose members are equal after normalization.</summary>
    Identical,

    /// <summary>Groups with one member.</summary>
    Unique,

    /// <summary>Groups with an item that holds secret findings.</summary>
    HasFindings,
}
