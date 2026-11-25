namespace SharedKernel.Vault.Catalog;

/// <summary>Whether a file matches the source recorded in a catalog.</summary>
public enum CatalogSourceMatch
{
    /// <summary>The file name and SHA-256 digest match.</summary>
    Match,

    /// <summary>The file name differs from the recorded name.</summary>
    FileNameMismatch,

    /// <summary>The name matches and the SHA-256 digest differs.</summary>
    DigestMismatch,
}
