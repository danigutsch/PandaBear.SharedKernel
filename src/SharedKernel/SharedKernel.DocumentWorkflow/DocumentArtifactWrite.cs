using System.Security.Cryptography;
using PandaBear.Review;

namespace PandaBear.DocumentWorkflow;

/// <summary>Describes the bounded renderer output to store immutably for an approved draft.</summary>
public sealed class DocumentArtifactWrite
{
    /// <summary>Captures one artifact write for the approved revision.</summary>
    /// <param name="revision">The approved revision that produced the artifact.</param>
    /// <param name="contentType">The host-selected media type.</param>
    /// <param name="content">The renderer output supplied for the duration of the write.</param>
    internal DocumentArtifactWrite(DocumentRevision revision, string contentType, ReadOnlyMemory<byte> content)
    {
        LineageId = revision.LineageId;
        RevisionNumber = revision.Number;
        Reviewed = revision.Review.Revision;
        ContentType = contentType;
        Content = content;
        ContentHash = Convert.ToHexString(SHA256.HashData(content.Span));
    }

    /// <summary>Gets the document lineage that owns the artifact.</summary>
    public Guid LineageId { get; }

    /// <summary>Gets the document revision that owns the artifact.</summary>
    public int RevisionNumber { get; }

    /// <summary>Gets the exact approved snapshot used to render the artifact.</summary>
    public ReviewRevision Reviewed { get; }

    /// <summary>Gets the host-configured media type.</summary>
    public string ContentType { get; }

    /// <summary>Gets the uppercase hexadecimal SHA-256 digest of the renderer output.</summary>
    public string ContentHash { get; }

    /// <summary>Gets the bytes available for the write operation.</summary>
    public ReadOnlyMemory<byte> Content { get; }
}
