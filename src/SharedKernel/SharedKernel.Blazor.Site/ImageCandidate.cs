using System.Globalization;
using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site;

/// <summary>
/// One entry of a <c>srcset</c>: an image address with a width descriptor (<c>640w</c>) or a pixel-density descriptor
/// (<c>2x</c>). The address must be root-relative or <c>https</c> (<see cref="LinkTarget.IsImageSource" />).
/// </summary>
public sealed class ImageCandidate : ValueObject
{
    private ImageCandidate(LinkTarget src, int? width, decimal? density)
    {
        Src = src;
        Width = width;
        Density = density;
    }

    /// <summary>Gets the image address.</summary>
    public LinkTarget Src { get; }

    /// <summary>Gets the intrinsic width in pixels of this file, or <see langword="null" /> for a density descriptor.</summary>
    public int? Width { get; }

    /// <summary>
    /// Gets the pixel density this file is for, rounded to three decimals as it is written in the <c>srcset</c>, or <see langword="null" />
    /// for a width descriptor.
    /// </summary>
    public decimal? Density { get; }

    /// <summary>Creates a candidate with a width descriptor, which lets the browser pick by the rendered size given in <c>sizes</c>.</summary>
    /// <param name="src">The address of a file of this width.</param>
    /// <param name="width">The file's intrinsic width in pixels.</param>
    /// <returns>The candidate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="src" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="src" /> is not root-relative or https, or starts or ends with a comma.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width" /> is not positive.</exception>
    public static ImageCandidate AtWidth(LinkTarget src, int width)
    {
        RequireImageSource(src);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        return new ImageCandidate(src, width, density: null);
    }

    /// <summary>Creates a candidate with a pixel-density descriptor, for an image shown at one fixed size.</summary>
    /// <param name="src">The address of a file for this density.</param>
    /// <param name="density">The device pixel ratio, such as 1, 1.5 or 2; it is rounded to three decimals.</param>
    /// <returns>The candidate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="src" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="src" /> is not root-relative or https, or starts or ends with a comma.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="density" /> is not positive once rounded to three decimals.</exception>
    public static ImageCandidate AtDensity(LinkTarget src, decimal density)
    {
        RequireImageSource(src);
        var written = Math.Round(density, 3, MidpointRounding.AwayFromZero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(written, nameof(density));
        return new ImageCandidate(src, width: null, written);
    }

    /// <summary>Returns the candidate as written in a <c>srcset</c>, such as <c>/a-640.jpg 640w</c> or <c>/a@2x.jpg 2x</c>.</summary>
    /// <returns>The candidate string.</returns>
    public override string ToString()
    {
        return Width is { } width
            ? string.Create(CultureInfo.InvariantCulture, $"{Src.Value} {width}w")
            : string.Create(CultureInfo.InvariantCulture, $"{Src.Value} {Density!.Value.ToString("0.###", CultureInfo.InvariantCulture)}x");
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Src;
        yield return Width;
        yield return Density;
    }

    private static void RequireImageSource(LinkTarget src)
    {
        ArgumentNullException.ThrowIfNull(src);
        if (!src.IsImageSource)
        {
            throw new ArgumentException("An image address must be root-relative or https.", nameof(src));
        }

        // A srcset splits candidates on commas that follow an address, so an address may not start or end with one.
        if (src.Value.StartsWith(',') || src.Value.EndsWith(','))
        {
            throw new ArgumentException("An image candidate address must not start or end with a comma.", nameof(src));
        }
    }
}
