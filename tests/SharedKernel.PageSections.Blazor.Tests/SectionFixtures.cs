using System.Net;
using System.Text.RegularExpressions;

namespace PandaBear.PageSections.Blazor.Tests;

/// <summary>Builds the sections, the image and link resolution and the renders that the page section tests share.</summary>
internal static partial class SectionFixtures
{
    /// <summary>The image that resolves to a described photo, <c>/media/1.webp</c>.</summary>
    public const int Photo = 1;

    /// <summary>An image the application no longer has, which resolves to nothing.</summary>
    public const int Missing = 99;

    /// <summary>A link target the application no longer has, which resolves to nothing.</summary>
    public const string LostTarget = "lost";

    /// <summary>The video hint format, in Portuguese like the text.</summary>
    public const string HintFormat = "(abre em {0})";

    /// <summary>Resolves an image id to a 800 by 600 photo at <c>/media/{id}.webp</c> described as "Photo {id}", or nothing for <see cref="Missing" />.</summary>
    /// <param name="id">The image id.</param>
    /// <returns>The image, or <see langword="null" />.</returns>
    public static ResponsiveImage? Image(int id) => id == Missing
        ? null
        : ResponsiveImage.Create(LinkTarget.FromString($"/media/{id}.webp"), 800, 600, ImageAlternative.Describe($"Photo {id}"));

    /// <summary>Resolves a link target to <c>/pt/{target}</c>, or nothing for <see cref="LostTarget" />.</summary>
    /// <param name="target">The target.</param>
    /// <returns>The address, or <see langword="null" />.</returns>
    public static LinkTarget? Link(string target) => target == LostTarget ? null : LinkTarget.FromString("/pt/" + target);

    /// <summary>Restores a stored section, its link target unchanged.</summary>
    /// <typeparam name="TExtra">The extension.</typeparam>
    /// <param name="definition">The definition.</param>
    /// <returns>The section.</returns>
    public static PageSection<int, string, TExtra> Section<TExtra>(PageSectionDefinition<int, string, TExtra> definition)
        where TExtra : struct, IEquatable<TExtra> => PageSection.Restore(definition, target => target);

    /// <summary>Restores a stored section without an extension.</summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The section.</returns>
    public static PageSection<int, string, NoExtra> Plain(PageSectionDefinition<int, string, NoExtra> definition) => Section(definition);

    /// <summary>A section with every part: an image band, an eyebrow, a heading, two paragraphs, two questions, a person, two quotes, a video, a button and one image beside the text.</summary>
    /// <returns>The section.</returns>
    public static PageSection<int, string, NoExtra> Rich() => Plain(new PageSectionDefinition<int, string, NoExtra>
    {
        Eyebrow = "Destino",
        Heading = "Explore a China",
        Text = "Primeiro parágrafo.\n\nSegundo parágrafo.",
        Tone = SectionTone.Image,
        BackgroundImageId = Photo,
        Person = new PagePersonDefinition<int>(Photo, "Ana Exemplo", "Fundadora"),
        Quotes = [new PageQuoteDefinition("Viajar é viver.", "Ana Exemplo"), new PageQuoteDefinition("Pedalar é liberdade.", null)],
        ImageIds = [Photo],
        ImageLayout = SectionImageLayout.BesideText,
        Video = new PageVideoDefinition<int>("https://www.youtube.com/watch?v=abc123", Photo, "Nossa viagem"),
        Link = new PageLinkDefinition<string>("Saiba mais", "viagens"),
        Questions = [new PageQuestionDefinition("Preciso de bicicleta?", "Não.\n\nAlugamos uma para você."), new PageQuestionDefinition("Há carro de apoio?", "Sim.")],
    });

    /// <summary>Renders sections with the fixtures' image and link resolution and the Portuguese video hint.</summary>
    /// <typeparam name="TExtra">The extension.</typeparam>
    /// <param name="context">The bUnit context.</param>
    /// <param name="sections">The sections.</param>
    /// <param name="configure">More parameters.</param>
    /// <returns>The rendered sections.</returns>
    public static IRenderedComponent<PageSectionsView<int, string, TExtra>> RenderSections<TExtra>(
        this BunitContext context,
        IReadOnlyList<PageSection<int, string, TExtra>> sections,
        Action<ComponentParameterCollectionBuilder<PageSectionsView<int, string, TExtra>>>? configure = null)
        where TExtra : struct, IEquatable<TExtra> => context.Render<PageSectionsView<int, string, TExtra>>(parameters =>
        {
            parameters
                .Add(view => view.Sections, sections)
                .Add(view => view.Image, Image)
                .Add(view => view.Link, Link)
                .Add(view => view.VideoSiteHintFormat, HintFormat);
            configure?.Invoke(parameters);
        });

    /// <summary>Gets the rendered markup decoded and without the white space between tags, so it can be compared as one line.</summary>
    /// <typeparam name="TExtra">The extension.</typeparam>
    /// <param name="cut">The rendered sections.</param>
    /// <returns>The markup.</returns>
    public static string Html<TExtra>(IRenderedComponent<PageSectionsView<int, string, TExtra>> cut)
        where TExtra : struct, IEquatable<TExtra> => Compact(WebUtility.HtmlDecode(cut.Markup));

    /// <summary>Removes the white space between tags, so markup can be compared as one line.</summary>
    /// <param name="markup">The markup.</param>
    /// <returns>The compact markup.</returns>
    public static string Compact(string markup) => BetweenTags().Replace(markup, "><").Trim();

    [GeneratedRegex(@">\s+<")]
    private static partial Regex BetweenTags();
}
