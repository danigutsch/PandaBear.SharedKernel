using SharedKernel.Domain;
using SharedKernel.InputNormalization;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>
/// The accessibility decision for an image, as a domain stores it: a sanitized description of what the image shows, at most
/// <see cref="MaxLength"/> characters, or an explicit statement that the image is decorative (an empty text). A rendering layer turns it
/// into its own value, such as <c>ImageAlternative.From</c> in <c>SharedKernel.Blazor.Site</c>.
/// </summary>
public sealed class ImageAltText : ValueObject
{
    /// <summary>The longest description after sanitizing.</summary>
    public const int MaxLength = 500;

    private ImageAltText(string text) => Text = text;

    /// <summary>Gets the description; empty for a decorative image.</summary>
    public string Text { get; }

    /// <summary>Gets a value indicating whether the image is purely decorative and is rendered with an empty <c>alt</c>.</summary>
    public bool IsDecorative => Text.Length == 0;

    /// <summary>Creates the decision after sanitizing the text with <see cref="StringSanitizer"/>.</summary>
    /// <param name="text">What the image shows; required unless <paramref name="decorative"/>.</param>
    /// <param name="decorative">Whether the image is decorative; then the text must be blank.</param>
    /// <param name="field">The field path of the errors.</param>
    /// <returns>
    /// The decision, or one validation error on <paramref name="field"/> that never echoes the input: <c>A decorative image has no alt
    /// text.</c>, <c>Alt text is required unless the image is decorative.</c>, <c>Alt text is not valid text.</c> or <c>Alt text cannot
    /// exceed 500 characters.</c>
    /// </returns>
    public static Result<ImageAltText> Create(string? text, bool decorative, string field = "alt")
    {
        if (decorative)
        {
            return StringSanitizer.TrySanitize(text, out var blank) && string.IsNullOrEmpty(blank)
                ? Result.Ok(new ImageAltText(string.Empty))
                : Refuse(field, "A decorative image has no alt text.");
        }

        var errors = new ValidationErrors();
        var description = SanitizedText.Optional(errors, text, field, "Alt text", MaxLength, out var entered);
        if (errors.HasErrors)
        {
            return errors.ToResult<ImageAltText>();
        }

        return entered
            ? Result.Ok(new ImageAltText(description!))
            : Refuse(field, "Alt text is required unless the image is decorative.");
    }

    /// <summary>For persistence mappers: rebuilds a stored decision without validation, so never use it for input; an empty text is decorative.</summary>
    /// <param name="text">The stored text.</param>
    /// <returns>The decision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static ImageAltText Restore(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new ImageAltText(text);
    }

    /// <inheritdoc />
    public override string ToString() => Text;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Text;
    }

    private static Result<ImageAltText> Refuse(string field, string message) => Result.Invalid<ImageAltText>(detail: message, field: field, message: message);
}
