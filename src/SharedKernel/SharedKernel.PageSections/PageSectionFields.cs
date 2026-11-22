using System.Globalization;
using PandaBear.BuildingBlocks;

namespace PandaBear.PageSections;

/// <summary>
/// The field paths of section validation errors, ViajantesTurismo V2's. A section's fields follow its path (see <see cref="Section"/>), such
/// as <c>pt.sections[0].heading</c>, a question's its own, such as <c>pt.sections[0].questions[1].answer</c>, and a part's its part's, such as
/// <c>pt.sections[0].video.posterImageId</c>. An error of the section as a whole is on the section's path; one of the list on the prefix.
/// </summary>
public static class PageSectionFields
{
    /// <summary>The eyebrow.</summary>
    public const string Eyebrow = "eyebrow";

    /// <summary>The heading: too long, or missing on a subsection.</summary>
    public const string Heading = "heading";

    /// <summary>The level: undefined, or a subsection without a headed section before it.</summary>
    public const string Level = "level";

    /// <summary>The text.</summary>
    public const string Text = "text";

    /// <summary>The questions as a list; a question's fields are <c>questions[i].question</c> and <c>questions[i].answer</c>.</summary>
    public const string Questions = "questions";

    /// <summary>A question's question.</summary>
    public const string Question = "question";

    /// <summary>A question's answer.</summary>
    public const string Answer = "answer";

    /// <summary>The tone.</summary>
    public const string Tone = "tone";

    /// <summary>The background image.</summary>
    public const string BackgroundImageId = "backgroundImageId";

    /// <summary>The person line; its fields are <c>person.name</c>, <c>person.role</c> and <c>person.imageId</c>.</summary>
    public const string Person = "person";

    /// <summary>The person's name.</summary>
    public const string Name = "name";

    /// <summary>The person's role.</summary>
    public const string Role = "role";

    /// <summary>The person's photo.</summary>
    public const string ImageId = "imageId";

    /// <summary>The quotes as a list; a quote's fields are <c>quotes[i].text</c> and <c>quotes[i].cite</c>.</summary>
    public const string Quotes = "quotes";

    /// <summary>A quote's words.</summary>
    public const string QuoteText = "text";

    /// <summary>A quote's source.</summary>
    public const string Cite = "cite";

    /// <summary>The images.</summary>
    public const string ImageIds = "imageIds";

    /// <summary>The image layout.</summary>
    public const string ImageLayout = "imageLayout";

    /// <summary>The video; its fields are <c>video.address</c>, <c>video.posterImageId</c> and <c>video.title</c>.</summary>
    public const string Video = "video";

    /// <summary>The video's address.</summary>
    public const string Address = "address";

    /// <summary>The video's poster.</summary>
    public const string PosterImageId = "posterImageId";

    /// <summary>The video's title.</summary>
    public const string Title = "title";

    /// <summary>The button; its fields are <c>link.text</c>, <c>link.style</c> and the target's own, which the application's context names.</summary>
    public const string Link = "link";

    /// <summary>The button's text.</summary>
    public const string LinkText = "text";

    /// <summary>The button's style.</summary>
    public const string Style = "style";

    /// <summary>Builds the path of a section, such as <c>pt.sections[0]</c>.</summary>
    /// <param name="prefix">The path of the list, such as <c>pt.sections</c>.</param>
    /// <param name="index">The position of the section.</param>
    /// <returns>The path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <see langword="null"/>.</exception>
    public static string Section(string prefix, int index)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return string.Create(CultureInfo.InvariantCulture, $"{prefix}[{index}]");
    }

    /// <summary>Builds V2's path of the publish rule of a translation's images, such as <c>pt.images</c>, for <see cref="PageSectionScope.ImagesField"/>.</summary>
    /// <param name="culture">The culture of the translation.</param>
    /// <returns>The path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> is <see langword="null"/>.</exception>
    public static string Images(ContentCulture culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return culture.Value + ".images";
    }
}
