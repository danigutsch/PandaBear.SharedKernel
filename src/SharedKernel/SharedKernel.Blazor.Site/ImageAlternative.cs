using SharedKernel.BuildingBlocks;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// The accessibility decision for an image: a description for people who cannot see it, or an explicit statement that it is decorative.
/// Components that show an image take this instead of a loose alt string, so "forgot the alt text" cannot compile.
/// </summary>
public sealed class ImageAlternative : ValueObject
{
    private ImageAlternative(string text) => Text = text;

    /// <summary>Gets the description; empty for a decorative image.</summary>
    public string Text { get; }

    /// <summary>Gets a value indicating whether the image is purely decorative and is rendered with an empty <c>alt</c>.</summary>
    public bool IsDecorative => Text.Length == 0;

    /// <summary>Describes an image that carries information.</summary>
    /// <param name="text">What the image shows.</param>
    /// <returns>The alternative.</returns>
    /// <exception cref="ArgumentException"><paramref name="text" /> is null, empty or blank; use <see cref="Decorative" /> for a decorative image.</exception>
    public static ImageAlternative Describe(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new ImageAlternative(text.Trim());
    }

    /// <summary>States that an image is purely decorative.</summary>
    /// <returns>The alternative, with an empty text.</returns>
    public static ImageAlternative Decorative() => new(string.Empty);

    /// <inheritdoc />
    public override string ToString() => Text;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Text;
    }
}
