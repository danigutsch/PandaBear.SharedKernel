using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.ContentRights;

/// <summary>The rights statement explicitly recorded for a work.</summary>
public enum ContentLicenseKind
{
    /// <summary>The rights status has not been established.</summary>
    Unknown,

    /// <summary>The declaration reserves rights.</summary>
    [SuppressMessage("Naming", "CA1700:Do not name enum values Reserved",
        Justification = "Reserved rights means a rights holder reserves rights to the work.")]
    ReservedRights,

    /// <summary>Terms supplied by the rights holder outside the supported standard tools.</summary>
    CustomTerms,

    /// <summary>Separate permission described by the caller.</summary>
    Permission,

    /// <summary>Creative Commons Attribution.</summary>
    CcBy,

    /// <summary>Creative Commons Attribution-ShareAlike.</summary>
    CcBySa,

    /// <summary>Creative Commons Attribution-NoDerivatives.</summary>
    CcByNd,

    /// <summary>Creative Commons Attribution-NonCommercial.</summary>
    CcByNc,

    /// <summary>Creative Commons Attribution-NonCommercial-ShareAlike.</summary>
    CcByNcSa,

    /// <summary>Creative Commons Attribution-NonCommercial-NoDerivatives.</summary>
    CcByNcNd,

    /// <summary>The CC0 public domain dedication and fallback license.</summary>
    Cc0,

    /// <summary>The Public Domain Mark, which describes existing status.</summary>
    PublicDomainMark,
}
