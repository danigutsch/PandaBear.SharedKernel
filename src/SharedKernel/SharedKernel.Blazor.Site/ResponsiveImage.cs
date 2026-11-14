using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site;

/// <summary>
/// An image for <see cref="Hero" />, <see cref="Card" /> and <see cref="ImageGrid" />: a fallback address with its intrinsic
/// width and height, the accessibility decision, and optionally a <c>srcset</c> with <c>sizes</c> and <c>picture</c> sources
/// per format (AVIF, WebP, ...).
/// </summary>
/// <remarks>
/// Every address must be root-relative or https (<see cref="LinkTarget.IsImageSource" />), the width and height must be positive
/// so the layout reserves the space before the image loads, and <c>sizes</c> is required whenever a candidate uses a width
/// descriptor. Without sources the component renders one <c>img</c>; with sources it renders a <c>picture</c> whose
/// <c>source</c> elements share <see cref="Sizes" /> and whose <c>img</c> is the fallback.
/// </remarks>
public sealed class ResponsiveImage : ValueObject
{
    private const int MaxSizesLength = 1024;

    private ResponsiveImage(
        LinkTarget src,
        int width,
        int height,
        ImageAlternative alt,
        string? sizes,
        IReadOnlyList<ImageCandidate> srcSet,
        IReadOnlyList<ImageSource> sources)
    {
        Src = src;
        Width = width;
        Height = height;
        Alt = alt;
        Sizes = sizes;
        SrcSet = srcSet;
        Sources = sources;
    }

    /// <summary>Gets the fallback address, which every browser can show.</summary>
    public LinkTarget Src { get; }

    /// <summary>Gets the intrinsic width in pixels; with <see cref="Height" /> it sets the aspect ratio the layout reserves.</summary>
    public int Width { get; }

    /// <summary>Gets the intrinsic height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the accessibility decision: a description, or decorative.</summary>
    public ImageAlternative Alt { get; }

    /// <summary>Gets the <c>sizes</c> value, such as <c>(min-width: 48rem) 50vw, 100vw</c>, or <see langword="null" />.</summary>
    public string? Sizes { get; }

    /// <summary>Gets the <c>srcset</c> candidates of the <c>img</c>; empty for none.</summary>
    public IReadOnlyList<ImageCandidate> SrcSet { get; }

    /// <summary>Gets the <c>picture</c> sources in the order the browser considers them; empty for a plain <c>img</c>.</summary>
    public IReadOnlyList<ImageSource> Sources { get; }

    /// <summary>Gets a value indicating whether the image renders as a <c>picture</c>.</summary>
    public bool UsesPicture => Sources.Count > 0;

    /// <summary>Creates an image.</summary>
    /// <param name="src">The fallback address, root-relative or https.</param>
    /// <param name="width">The intrinsic width in pixels of the fallback.</param>
    /// <param name="height">The intrinsic height in pixels of the fallback.</param>
    /// <param name="alt">The accessibility decision.</param>
    /// <param name="sizes">The <c>sizes</c> value; required when a candidate uses a width descriptor.</param>
    /// <param name="srcSet">The <c>srcset</c> candidates of the <c>img</c>, if any.</param>
    /// <param name="sources">The <c>picture</c> sources, most efficient format first, if any.</param>
    /// <returns>The image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="src" /> or <paramref name="alt" /> is <see langword="null" />, or a source is.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="src" /> is not root-relative or https; <paramref name="sizes" /> is blank, too long, contains a control
    /// character, or is missing while a width descriptor is used; or <paramref name="srcSet" /> mixes or repeats descriptors.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width" /> or <paramref name="height" /> is not positive.</exception>
    public static ResponsiveImage Create(
        LinkTarget src,
        int width,
        int height,
        ImageAlternative alt,
        string? sizes = null,
        IEnumerable<ImageCandidate>? srcSet = null,
        IEnumerable<ImageSource>? sources = null)
    {
        ArgumentNullException.ThrowIfNull(src);
        if (!src.IsImageSource)
        {
            throw new ArgumentException("An image address must be root-relative or https.", nameof(src));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(alt);
        if (sizes is not null
            && (string.IsNullOrWhiteSpace(sizes) || sizes.Length > MaxSizesLength || sizes.Any(char.IsControl)))
        {
            throw new ArgumentException($"Sizes must be one line of at most {MaxSizesLength} characters.", nameof(sizes));
        }

        ImageCandidate[] candidates = srcSet is null ? [] : CandidateSet.Check(srcSet, nameof(srcSet));
        var sourceList = sources?.ToArray() ?? [];
        if (sourceList.Any(source => source is null))
        {
            throw new ArgumentNullException(nameof(sources), "A picture source must not be null.");
        }

        var widthDescriptors = candidates is [{ Width: not null }, ..] || sourceList.Any(source => source.UsesWidthDescriptors);
        if (widthDescriptors && sizes is null)
        {
            throw new ArgumentException("Sizes is required when a srcset uses width descriptors.", nameof(sizes));
        }

        return new ResponsiveImage(src, width, height, alt, sizes, candidates, sourceList);
    }

    /// <inheritdoc />
    public override string ToString() => Src.Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Src;
        yield return Width;
        yield return Height;
        yield return Alt;
        yield return Sizes;
        yield return SrcSet.Count;
        foreach (var candidate in SrcSet)
        {
            yield return candidate;
        }

        foreach (var source in Sources)
        {
            yield return source;
        }
    }
}
