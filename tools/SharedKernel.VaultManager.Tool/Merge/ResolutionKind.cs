namespace PandaBear.VaultManager.Tool.Merge;

/// <summary>How a changed field was resolved.</summary>
internal enum ResolutionKind
{
    /// <summary>Values were equal after normalization.</summary>
    Equal = 0,

    /// <summary>Only some members held a value, and it was taken.</summary>
    OneSided = 1,

    /// <summary>Collections were unioned.</summary>
    Union = 2,

    /// <summary>The newest member's value won.</summary>
    Newest = 3,

    /// <summary>The preferred source's value won.</summary>
    PreferredSource = 4,

    /// <summary>A user decision chose the value.</summary>
    Decision = 5,

    /// <summary>The field needs a user decision.</summary>
    Unresolved = 6,
}
