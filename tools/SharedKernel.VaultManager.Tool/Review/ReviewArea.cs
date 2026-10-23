namespace PandaBear.VaultManager.Tool.Review;

/// <summary>Where a key of the review UI works.</summary>
internal enum ReviewArea
{
    /// <summary>Anywhere in the review window.</summary>
    Everywhere = 0,

    /// <summary>In the groups list.</summary>
    Groups,

    /// <summary>In the field table of the selected group.</summary>
    Fields,

    /// <summary>In the findings table of the selected group.</summary>
    Findings,
}
