using SharedKernel.BuildingBlocks;

namespace SharedKernel.PageSections;

/// <summary>Where one translation's sections are validated: its culture, whether it is published, and the paths of its errors.</summary>
/// <param name="Culture">The culture of the translation.</param>
/// <param name="Published">Whether the translation is published.</param>
/// <param name="FieldPrefix">The path of the list, such as <c>pt.sections</c>; each section's errors are under <c>{FieldPrefix}[i]</c>.</param>
/// <param name="ImagesField">The path of the publish rule of the images, such as V2's <c>pt.images</c> (see <see cref="PageSectionFields.Images"/>).</param>
public sealed record PageSectionScope(ContentCulture Culture, bool Published, string FieldPrefix, string ImagesField)
{
    /// <summary>Gets the culture of the translation.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public ContentCulture Culture { get; } = Culture ?? throw new ArgumentNullException(nameof(Culture));

    /// <summary>Gets the path of the list.</summary>
    /// <exception cref="ArgumentException">The value is <see langword="null"/>, empty or blank.</exception>
    public string FieldPrefix { get; } = Required(FieldPrefix, nameof(FieldPrefix));

    /// <summary>Gets the path of the publish rule of the images.</summary>
    /// <exception cref="ArgumentException">The value is <see langword="null"/>, empty or blank.</exception>
    public string ImagesField { get; } = Required(ImagesField, nameof(ImagesField));

    /// <summary>
    /// Gets a value indicating whether a published translation's images are checked for eligibility in its culture. The default is
    /// <see langword="true"/>. Turn it off when the application checks the images itself with
    /// <see cref="PageSectionList.FindIneligible{TImageId, TLink, TExtra}"/>, together with its own images such as a page cover, and words
    /// its own message (V2 names the files).
    /// </summary>
    public bool CheckImageEligibility { get; init; } = true;

    private static string Required(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value;
    }
}
