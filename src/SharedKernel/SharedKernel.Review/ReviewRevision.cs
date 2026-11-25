namespace PandaBear.Review;

/// <summary>Identifies a version of content within a review history.</summary>
public sealed record ReviewRevision
{
    private const int Sha256HexLength = 64;

    /// <summary>Creates the identity of a content revision.</summary>
    /// <param name="number">The positive, increasing review revision number.</param>
    /// <param name="contentHash">The SHA-256 digest of the exact content presented to reviewers.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="number"/> is not positive.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentHash"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="contentHash"/> is not a 64-character hexadecimal digest.</exception>
    public ReviewRevision(long number, string contentHash)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ArgumentNullException.ThrowIfNull(contentHash);
        if (contentHash.Length != Sha256HexLength || !contentHash.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("A SHA-256 content hash must contain 64 hexadecimal characters.", nameof(contentHash));
        }

        Number = number;
        ContentHash = contentHash.ToUpperInvariant();
    }

    /// <summary>Gets the increasing number of the content revision.</summary>
    public long Number { get; }

    /// <summary>Gets the uppercase hexadecimal SHA-256 digest of the reviewed content.</summary>
    public string ContentHash { get; }
}
