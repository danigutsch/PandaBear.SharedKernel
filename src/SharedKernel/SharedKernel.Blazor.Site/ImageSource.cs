using System.Buffers;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// One <c>source</c> of a <c>picture</c>: a media type, such as <c>image/avif</c> or <c>image/webp</c>, and the files in that
/// format. A browser takes the first source whose type it supports, so list the most efficient format first.
/// </summary>
public sealed class ImageSource : ValueObject
{
    private const string ImagePrefix = "image/";

    private static readonly SearchValues<char> SubtypeCharacters = SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789.+-");

    private ImageSource(string type, IReadOnlyList<ImageCandidate> candidates)
    {
        Type = type;
        Candidates = candidates;
    }

    /// <summary>Gets the media type, in lower case.</summary>
    public string Type { get; }

    /// <summary>Gets the files in this format.</summary>
    public IReadOnlyList<ImageCandidate> Candidates { get; }

    /// <summary>Gets a value indicating whether the candidates use width descriptors, which require <c>sizes</c>.</summary>
    public bool UsesWidthDescriptors => Candidates[0].Width is not null;

    /// <summary>Gets the <c>srcset</c> value.</summary>
    public string SrcSet => string.Join(", ", Candidates);

    /// <summary>Creates a source.</summary>
    /// <param name="type">An image media type such as <c>image/avif</c>; it is trimmed and lower-cased.</param>
    /// <param name="candidates">The files in this format: at least one, all with width or all with density descriptors, none repeated.</param>
    /// <returns>The source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type" /> or <paramref name="candidates" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The type is not an <c>image/</c> media type, or the candidates are empty, mixed or repeated.</exception>
    public static ImageSource Create(string type, IEnumerable<ImageCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(type);
#pragma warning disable CA1308 // Lower case is the canonical form of a case-insensitive media type, not a security normalization.
        var normalized = type.Trim().ToLowerInvariant();
#pragma warning restore CA1308
        if (!IsImageMediaType(normalized))
        {
            throw new ArgumentException("A picture source type must be an image media type such as image/avif.", nameof(type));
        }

        return new ImageSource(normalized, CandidateSet.Check(candidates, nameof(candidates)));
    }

    /// <inheritdoc />
    public override string ToString() => $"{Type}: {SrcSet}";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        foreach (var candidate in Candidates)
        {
            yield return candidate;
        }
    }

    private static bool IsImageMediaType(string type)
    {
        return type.Length is > 6 and <= 64
            && type.StartsWith(ImagePrefix, StringComparison.Ordinal)
            && !type.AsSpan(ImagePrefix.Length).ContainsAnyExcept(SubtypeCharacters)
            && char.IsAsciiLetterOrDigit(type[ImagePrefix.Length]);
    }
}
