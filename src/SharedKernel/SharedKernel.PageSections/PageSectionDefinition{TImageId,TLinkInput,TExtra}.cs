namespace SharedKernel.PageSections;

/// <summary>
/// An unvalidated page section, and the round-trip form of a stored one (see <see cref="PageSection{TImageId, TLink, TExtra}.ToDefinition{TLinkInput}"/>).
/// Every part is optional, but a section must show more than its heading. Lists that are <see langword="null"/> are empty.
/// </summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TLinkInput">The application's raw link target, as an editor enters it or as it is stored.</typeparam>
/// <typeparam name="TExtra">The application's extension; <c>default</c> means none (see <see cref="NoExtra"/>).</typeparam>
public sealed record PageSectionDefinition<TImageId, TLinkInput, TExtra>
    where TImageId : struct, IEquatable<TImageId>
    where TLinkInput : class
    where TExtra : struct, IEquatable<TExtra>
{
    /// <summary>Gets the optional short line above the heading.</summary>
    public string? Eyebrow { get; init; }

    /// <summary>Gets the optional heading; a <see cref="SectionLevel.Subsection"/> needs one.</summary>
    public string? Heading { get; init; }

    /// <summary>Gets the level of the heading.</summary>
    public SectionLevel Level { get; init; }

    /// <summary>Gets the optional text; paragraphs are separated by blank lines.</summary>
    public string? Text { get; init; }

    /// <summary>Gets the frequently asked questions.</summary>
    public IReadOnlyList<PageQuestionDefinition>? Questions { get; init; }

    /// <summary>Gets the optional person line.</summary>
    public PagePersonDefinition<TImageId>? Person { get; init; }

    /// <summary>Gets the quotes.</summary>
    public IReadOnlyList<PageQuoteDefinition>? Quotes { get; init; }

    /// <summary>Gets the application's extension, such as a settings block or a display condition.</summary>
    public TExtra Extra { get; init; }

    /// <summary>Gets the band the section sits on.</summary>
    public SectionTone Tone { get; init; }

    /// <summary>Gets the background image; required by <see cref="SectionTone.Image"/> and allowed only with it.</summary>
    public TImageId? BackgroundImageId { get; init; }

    /// <summary>Gets the images, in order, each once.</summary>
    public IReadOnlyList<TImageId>? ImageIds { get; init; }

    /// <summary>Gets how the images are laid out.</summary>
    public SectionImageLayout ImageLayout { get; init; }

    /// <summary>Gets the optional video.</summary>
    public PageVideoDefinition<TImageId>? Video { get; init; }

    /// <summary>Gets the optional button.</summary>
    public PageLinkDefinition<TLinkInput>? Link { get; init; }
}
