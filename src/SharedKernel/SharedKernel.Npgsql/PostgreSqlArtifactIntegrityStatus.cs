namespace PandaBear.Npgsql;

/// <summary>The outcomes of comparing a stored artifact with its recorded metadata.</summary>
public enum PostgreSqlArtifactIntegrityStatus
{
    /// <summary>The body row or its bytes are missing.</summary>
    MissingBody,

    /// <summary>The body exists and its recorded length or digest is absent.</summary>
    MissingIntegrityMetadata,

    /// <summary>The recorded length is nonpositive or differs from the stored byte length.</summary>
    LengthMismatch,

    /// <summary>The length matches and the recorded digest differs from the digest of the stored bytes.</summary>
    DigestMismatch,

    /// <summary>The stored bytes match both the recorded length and digest.</summary>
    Intact,
}
