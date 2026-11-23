using System.Globalization;
using Microsoft.AspNetCore.Components;
using PandaBear.Blazor.Site;
using PandaBear.BuildingBlocks;

namespace PandaBear.PageSections.Blazor;

/// <summary>
/// Renders the sections of a page with the site components, each on the <see cref="Band" /> of its tone and its parts in the model's
/// fixed order: the heading (an <c>hgroup</c> with the eyebrow), the paragraphs, the questions (each a closed
/// <see cref="Disclosure" />), the person (<see cref="Byline" />), the quotes, the application's <see cref="Extra" />, the
/// <see cref="VideoLink" /> and the <see cref="ButtonLink" />, then the images as a mosaic or beside the parts in a <see cref="Split" />.
/// A section with a heading is a <see cref="Section" /> named by it, a subsection one with a level-three heading and no divider; one
/// without a heading is its parts alone on the band. A part the application cannot resolve, such as a deleted image or a link target
/// that no longer exists, is left out, and a section with nothing left to show renders nothing.
/// </summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TLink">The application's validated link target.</typeparam>
/// <typeparam name="TExtra">The application's extension; <c>default</c> means none.</typeparam>
public sealed partial class PageSectionsView<TImageId, TLink, TExtra>
    where TImageId : struct, IEquatable<TImageId>
    where TLink : notnull
    where TExtra : struct, IEquatable<TExtra>
{
    /// <summary>The default <see cref="HeadingIdFormat" />: <c>section-1</c> for the first section of the page.</summary>
    public const string DefaultHeadingIdFormat = "section-{0}";

    private const int SubsectionHeadingLevel = 3;

    private const int SectionHeadingLevel = 2;

    /// <summary>Gets or sets the sections, in page order.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<PageSection<TImageId, TLink, TExtra>> Sections { get; set; } = [];

    /// <summary>
    /// Gets or sets how an image id becomes an image: the background of an image band, a person's photo, an image of the mosaic and the
    /// video's poster. <see langword="null" /> leaves that image out: an image band without its background becomes a primary band, a
    /// byline shows no photo, and a video without its poster is left out. The poster is shown as decoration, because the video's title
    /// names its link, and must be self-hosted at root-relative addresses (<see cref="VideoLink.Poster" />). A video whose stored address
    /// is not a valid link is left out too.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Func<TImageId, ResponsiveImage?> Image { get; set; } = null!;

    /// <summary>
    /// Gets or sets how a section's link target becomes an address, such as the path of a page in the current language.
    /// <see langword="null" /> leaves the button out, for example when the target no longer exists.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Func<TLink, LinkTarget?> Link { get; set; } = null!;

    /// <summary>
    /// Gets or sets how the application's extension renders, between the quotes and the video. It renders only for a section whose
    /// <see cref="PageSection{TImageId, TLink, TExtra}.Extra" /> is not <c>default</c>, and such an extension counts as something to
    /// show. <see langword="null" />, the default, renders no extension and does not count it.
    /// </summary>
    [Parameter]
    public RenderFragment<TExtra>? Extra { get; set; }

    /// <summary>
    /// Gets or sets which sections are shown, for example by a display condition in the extension. <see langword="null" />, the default,
    /// shows every section. A hidden section keeps its place in the heading ids, so the ids of the others do not change.
    /// </summary>
    [Parameter]
    public Func<PageSection<TImageId, TLink, TExtra>, bool>? IsShown { get; set; }

    /// <summary>
    /// Gets or sets the composite format of a heading's id, with <c>{0}</c> the section's position on the page from 1. The default is
    /// <see cref="DefaultHeadingIdFormat" />; set another one when two lists of sections share a page. The id must not hold white space,
    /// and the format must use the position, so every heading has its own id.
    /// </summary>
    [Parameter]
    public string HeadingIdFormat { get; set; } = DefaultHeadingIdFormat;

    /// <summary>
    /// Gets or sets the composite format of the visually hidden hint after a video's title, with <c>{0}</c> the video's host without
    /// <c>www.</c>, for example <c>"(opens {0})"</c>, which reads "(opens youtube.com)". Write it in the language of the text.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string VideoSiteHintFormat { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language of the sections' text when it is not the page's, such as a fallback translation. It is written as
    /// <c>lang</c> on the element around the sections (WCAG 3.1.2). <see langword="null" />, the default, writes none.
    /// </summary>
    [Parameter]
    public ContentCulture? TextLanguage { get; set; }

    /// <summary>
    /// Gets or sets the alignment of every band's text, as <see cref="Band.TextAlignment" />. <see langword="null" />, the default,
    /// inherits the nearest aligned ancestor; <see cref="SiteAlignment.Center" /> centres the headings, the text, the byline, the video
    /// link and the button.
    /// </summary>
    [Parameter]
    public SiteAlignment? TextAlignment { get; set; }

    /// <summary>
    /// Gets or sets how the parts and the images of a section without a heading lie beside each other, as <see cref="Split.Alignment" />.
    /// The default is <see cref="SiteAlignment.Start" />; <see cref="SiteAlignment.Center" /> centres a short tagline beside a photo. A
    /// section with a heading always starts both columns at the top.
    /// </summary>
    [Parameter]
    public SiteAlignment SplitAlignment { get; set; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><see cref="Sections" />, a section, <see cref="Image" /> or <see cref="Link" /> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <see cref="VideoSiteHintFormat" /> is blank, or <see cref="HeadingIdFormat" /> is blank, gives an id with white space or gives
    /// every section the same id.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="TextAlignment" /> or <see cref="SplitAlignment" /> is not defined.</exception>
    /// <exception cref="FormatException">A format is not a valid composite format.</exception>
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Sections);
        ArgumentNullException.ThrowIfNull(Image);
        ArgumentNullException.ThrowIfNull(Link);
        ArgumentException.ThrowIfNullOrWhiteSpace(VideoSiteHintFormat);
        ArgumentException.ThrowIfNullOrWhiteSpace(HeadingIdFormat);
        foreach (var section in Sections)
        {
            ArgumentNullException.ThrowIfNull(section, nameof(Sections));
        }

        if (HeadingId(0).AsSpan().ContainsAnyWhiteSpace())
        {
            throw new ArgumentException("A heading id must not contain white space.", nameof(HeadingIdFormat));
        }

        if (string.Equals(HeadingId(0), HeadingId(1), StringComparison.Ordinal))
        {
            throw new ArgumentException("The heading id format must give every section its own id; put the position, {0}, in it.", nameof(HeadingIdFormat));
        }

        _ = string.Format(CultureInfo.InvariantCulture, VideoSiteHintFormat, string.Empty);
        if (TextAlignment is { } alignment && !Enum.IsDefined(alignment))
        {
            throw new ArgumentOutOfRangeException(nameof(TextAlignment), alignment, "The text alignment is not defined.");
        }

        if (!Enum.IsDefined(SplitAlignment))
        {
            throw new ArgumentOutOfRangeException(nameof(SplitAlignment), SplitAlignment, "The split alignment is not defined.");
        }
    }

    private static string SiteName(Uri address)
    {
        var host = address.Host;
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    private static ButtonVariant Variant(LinkStyle style) => style switch
    {
        LinkStyle.Secondary => ButtonVariant.Secondary,
        LinkStyle.Outline => ButtonVariant.Outline,
        _ => ButtonVariant.Primary,
    };

    private static ResponsiveImage Decorative(ResponsiveImage image) =>
        image.Alt.IsDecorative
            ? image
            : ResponsiveImage.Create(
                image.Src,
                image.Width,
                image.Height,
                ImageAlternative.Decorative(),
                image.Sizes,
                image.SrcSet.Count == 0 ? null : image.SrcSet,
                image.Sources);

    private string HeadingId(int index) => string.Format(CultureInfo.InvariantCulture, HeadingIdFormat, index + 1);

    private string VideoHint(Uri address) => string.Format(CultureInfo.InvariantCulture, VideoSiteHintFormat, SiteName(address));

    private VideoView? ResolveVideo(PageVideo<TImageId>? video)
    {
        if (video is null
            || !LinkTarget.Create(video.Address.Value).TryGetValue(out var address)
            || !Uri.TryCreate(address.Value, UriKind.Absolute, out var uri)
            || Image(video.PosterImageId) is not { } poster)
        {
            return null;
        }

        return new VideoView(address, video.Title, Decorative(poster), VideoHint(uri));
    }

    private SectionView View(PageSection<TImageId, TLink, TExtra> section)
    {
        var images = new List<GalleryImage>(section.ImageIds.Count);
        foreach (var id in section.ImageIds)
        {
            if (Image(id) is { } image)
            {
                images.Add(new GalleryImage(image));
            }
        }

        var background = section.Tone == SectionTone.Image && section.BackgroundImageId is { } backgroundId ? Image(backgroundId) : null;
        var video = ResolveVideo(section.Video);
        var button = section.Link is { } link ? Link(link.Target) : null;
        var photo = section.Person?.ImageId is { } photoId ? Image(photoId) : null;
        var extra = Extra is not null && !section.Extra.Equals(default);
        var hasParts = section.Paragraphs.Count > 0
            || section.Questions.Count > 0
            || section.Person is not null
            || section.Quotes.Count > 0
            || extra
            || video is not null
            || button is not null;
        return new SectionView(images, background, video, button, photo, extra, hasParts);
    }

    private static BandTone Tone(PageSection<TImageId, TLink, TExtra> section, SectionView view) => section.Tone switch
    {
        SectionTone.Muted => BandTone.Muted,
        SectionTone.Primary => BandTone.Primary,
        SectionTone.Image => view.Background is null ? BandTone.Primary : BandTone.Image,
        _ => BandTone.Default,
    };

    private static int Level(PageSection<TImageId, TLink, TExtra> section) =>
        section.Level == SectionLevel.Subsection ? SubsectionHeadingLevel : SectionHeadingLevel;

    private static bool Beside(PageSection<TImageId, TLink, TExtra> section, SectionView view) =>
        section.ImageLayout == SectionImageLayout.BesideText && view.Images.Count > 0;

    /// <summary>The parts of a section that the application resolved.</summary>
    /// <param name="Images">The images that resolved, in order.</param>
    /// <param name="Background">The background of an image band, or <see langword="null" />.</param>
    /// <param name="Video">The video, or <see langword="null" /> when its address or poster does not resolve.</param>
    /// <param name="Button">The button's address, or <see langword="null" /> when the button is left out.</param>
    /// <param name="Photo">The person's photo, or <see langword="null" />.</param>
    /// <param name="ShowsExtra">Whether the extension renders.</param>
    /// <param name="HasParts">Whether any part beside the images is shown.</param>
    private sealed record SectionView(
        IReadOnlyList<GalleryImage> Images,
        ResponsiveImage? Background,
        VideoView? Video,
        LinkTarget? Button,
        ResponsiveImage? Photo,
        bool ShowsExtra,
        bool HasParts)
    {
        /// <summary>Gets whether the section shows anything.</summary>
        public bool HasContent => HasParts || Images.Count > 0;
    }

    /// <summary>A video whose address and poster resolved.</summary>
    /// <param name="Address">The address of the video.</param>
    /// <param name="Title">The title, which names the link.</param>
    /// <param name="Poster">The decorative poster.</param>
    /// <param name="Hint">The visually hidden hint that names the video's site.</param>
    private sealed record VideoView(LinkTarget Address, string Title, ResponsiveImage Poster, string Hint);
}
