using PandaBear.BuildingBlocks;
using static PandaBear.PageSections.Blazor.Tests.SectionFixtures;

namespace PandaBear.PageSections.Blazor.Tests;

/// <summary>
/// Tests how sections render: each on the band of its tone, its parts in the model's fixed order through the site components, a named
/// region only when it has a heading, a subsection's heading one level down, nothing for a section with nothing left to show, and the
/// language of the text kept.
/// </summary>
public sealed class PageSectionsViewTests : BunitContext
{
    [Fact]
    public void A_section_shows_its_parts_in_order_on_its_image_band_with_the_images_beside_the_text()
    {
        // Arrange & Act
        var main = Html(this.RenderSections([Rich()]));

        // Assert
        var band = main.IndexOf("<div class=\"site-band site-band--image\"><div class=\"site-band__inner\"><section class=\"site-section\" aria-labelledby=\"section-1\"><div class=\"site-split\"><div class=\"site-split__main\">", StringComparison.Ordinal);
        band.ShouldBeGreaterThan(-1);
        int[] order =
        [
            band,
            main.IndexOf("<p class=\"site-section__eyebrow\">Destino</p><h2 id=\"section-1\" class=\"site-section__title\">Explore a China</h2>", StringComparison.Ordinal),
            main.IndexOf("<p>Primeiro parágrafo.</p><p>Segundo parágrafo.</p>", StringComparison.Ordinal),
            main.IndexOf("<details class=\"site-disclosure\"><summary>Preciso de bicicleta?</summary><div class=\"site-disclosure__content\"><p>Não.</p><p>Alugamos uma para você.</p></div></details>", StringComparison.Ordinal),
            main.IndexOf("<details class=\"site-disclosure\"><summary>Há carro de apoio?</summary><div class=\"site-disclosure__content\"><p>Sim.</p></div></details>", StringComparison.Ordinal),
            main.IndexOf("<p class=\"site-byline__name\">Ana Exemplo</p><p class=\"site-byline__job-title\">Fundadora</p>", StringComparison.Ordinal),
            main.IndexOf("<blockquote>Viajar é viver.</blockquote><figcaption><cite>Ana Exemplo</cite></figcaption>", StringComparison.Ordinal),
            main.IndexOf("<blockquote>Pedalar é liberdade.</blockquote></figure>", StringComparison.Ordinal),
            main.IndexOf("<a href=\"https://www.youtube.com/watch?v=abc123\" class=\"site-video-link\" rel=\"noreferrer\">", StringComparison.Ordinal),
            main.IndexOf("<span class=\"site-video-link__title\">Nossa viagem</span><span class=\"site-visually-hidden\"> (abre em youtube.com)</span>", StringComparison.Ordinal),
            main.IndexOf("<p><a href=\"/pt/viagens\" class=\"site-button site-button--primary\">Saiba mais</a></p>", StringComparison.Ordinal),
            main.IndexOf("<div class=\"site-split__media\"><div class=\"site-image-grid site-image-grid--mosaic\">", StringComparison.Ordinal),
            main.IndexOf("<div class=\"site-band__media\"><img src=\"/media/1.webp\"", StringComparison.Ordinal),
        ];
        order.Min().ShouldBeGreaterThan(-1);
        order.ShouldBe([.. order.Order()]);
        main.ShouldContain("<span class=\"site-video-link__poster\"><img src=\"/media/1.webp\" alt=\"\"", StringComparison.Ordinal);
        main.ShouldContain("<div class=\"site-byline__photo\"><img src=\"/media/1.webp\" alt=\"Photo 1\"", StringComparison.Ordinal);
    }

    [Fact]
    public void A_section_of_only_a_heading_and_questions_shows_each_question_closed_and_a_subsection_has_a_level_three_heading_without_divider()
    {
        // Arrange
        PageSection<int, string, NoExtra>[] sections =
        [
            Plain(new() { Heading = "Perguntas frequentes", Questions = [new PageQuestionDefinition("Preciso de bicicleta?", "Não.")] }),
            Plain(new() { Heading = "Bagagem", Text = "Levamos as malas.", Level = SectionLevel.Subsection }),
        ];

        // Act
        var main = Html(this.RenderSections(sections));

        // Assert
        var faq = main.IndexOf("<h2 id=\"section-1\" class=\"site-section__title\">Perguntas frequentes</h2><div class=\"site-divider\" aria-hidden=\"true\"></div>", StringComparison.Ordinal);
        var question = main.IndexOf("<details class=\"site-disclosure\"><summary>Preciso de bicicleta?</summary><div class=\"site-disclosure__content\"><p>Não.</p></div></details>", StringComparison.Ordinal);
        var subsection = main.IndexOf(
            "<section class=\"site-section\" aria-labelledby=\"section-2\"><div class=\"site-section__header\"><h3 id=\"section-2\" class=\"site-section__title\">Bagagem</h3></div>"
            + "<div class=\"site-section__body\"><p>Levamos as malas.</p>",
            StringComparison.Ordinal);
        faq.ShouldBeGreaterThan(-1);
        question.ShouldBeGreaterThan(faq);
        subsection.ShouldBeGreaterThan(question);
        main.ShouldNotContain("<details class=\"site-disclosure\" open", StringComparison.Ordinal);
    }

    [Fact]
    public void Images_in_a_mosaic_follow_the_parts_inside_the_section_on_its_muted_band()
    {
        // Arrange
        var section = Plain(new() { Heading = "Fotos", Text = "Algumas fotos.", Tone = SectionTone.Muted, ImageIds = [Photo, 2] });

        // Act
        var main = Html(this.RenderSections([section]));

        // Assert
        var band = main.IndexOf("<div class=\"site-band site-band--muted\"><div class=\"site-band__inner\"><section class=\"site-section\" aria-labelledby=\"section-1\">", StringComparison.Ordinal);
        var text = main.IndexOf("<p>Algumas fotos.</p>", StringComparison.Ordinal);
        var mosaic = main.IndexOf("<div class=\"site-image-grid site-image-grid--mosaic\"><figure class=\"site-image-grid__item\"><img src=\"/media/1.webp\"", StringComparison.Ordinal);
        var second = main.IndexOf("<img src=\"/media/2.webp\"", StringComparison.Ordinal);
        var end = main.IndexOf("</figure></div></div></section></div></div>", mosaic, StringComparison.Ordinal);
        band.ShouldBeGreaterThan(-1);
        text.ShouldBeGreaterThan(band);
        mosaic.ShouldBeGreaterThan(text);
        second.ShouldBeGreaterThan(mosaic);
        end.ShouldBeGreaterThan(second);
    }

    [Theory]
    [InlineData("Fotos", SectionImageLayout.Mosaic, "section > .site-section__body > .site-image-grid")]
    [InlineData("Fotos", SectionImageLayout.BesideText, "section > .site-split > .site-split__media > .site-image-grid")]
    [InlineData(null, SectionImageLayout.Mosaic, ".site-band__inner > .site-image-grid")]
    [InlineData(null, SectionImageLayout.BesideText, ".site-band__inner > .site-split > .site-split__media > .site-image-grid")]
    public void Every_image_of_a_section_shows_its_caption_and_credit_under_it_in_each_layout(string? heading, SectionImageLayout layout, string grid)
    {
        // Arrange
        var section = Plain(new() { Heading = heading, Text = "Algumas fotos.", ImageIds = [Photo, 2], ImageLayout = layout });

        // Act
        var cut = this.RenderSections([section], parameters => parameters
            .Add(view => view.ImageCaption, Caption)
            .Add(view => view.ImageCredit, Credit));

        // Assert
        cut.FindAll($"{grid} > figure.site-image-grid__item > figcaption").Select(caption => Compact(caption.InnerHtml))
            .ShouldBe([CaptionMarkup(Photo), CaptionMarkup(2)]);
        cut.FindAll($"{grid} > figure > figcaption a").Select(link => link.GetAttribute("rel")).ShouldBe(["license", "license"]);
    }

    [Fact]
    public void The_background_the_photo_and_the_decorative_poster_stay_without_a_caption_or_credit()
    {
        // Arrange & Act
        var cut = this.RenderSections([Rich()], parameters => parameters
            .Add(view => view.ImageCaption, Caption)
            .Add(view => view.ImageCredit, Credit));

        // Assert
        var main = Html(cut);
        main.Split("Legenda 1", StringSplitOptions.None).Length.ShouldBe(2);
        main.Split("Foto: Autor 1", StringSplitOptions.None).Length.ShouldBe(2);
        Compact(cut.Find(".site-split__media figcaption").InnerHtml).ShouldBe(CaptionMarkup(Photo));
        cut.Find(".site-video-link").TextContent.ShouldNotContain("Legenda", StringComparison.Ordinal);
        cut.Find(".site-video-link img").GetAttribute("alt").ShouldBe(string.Empty);
        cut.FindAll(".site-band__media figcaption, .site-band__media .site-image-credit").ShouldBeEmpty();
        cut.Find(".site-byline").TextContent.ShouldNotContain("Legenda", StringComparison.Ordinal);
        cut.FindAll(".site-byline .site-image-credit").ShouldBeEmpty();
    }

    [Fact]
    public void An_image_shows_only_what_resolves_and_without_resolvers_no_image_has_a_caption()
    {
        // Arrange
        var section = Plain(new() { ImageIds = [Photo, 2, 3] });

        // Act
        var resolved = this.RenderSections([section], parameters => parameters
            .Add(view => view.ImageCaption, id => id switch { Photo => "Só legenda", 2 => " ", _ => null })
            .Add(view => view.ImageCredit, id => id == 3 ? new ImageCreditLine("Foto: Ana Lima") : null));
        var plain = this.RenderSections([section]);

        // Assert
        resolved.FindAll(".site-image-grid figure").Select(figure => figure.QuerySelector("figcaption")?.InnerHtml).ShouldBe(
        [
            "Só legenda",
            null,
            "<small class=\"site-image-credit\">Foto: Ana Lima</small>",
        ]);
        plain.FindAll(".site-image-grid figure").Count.ShouldBe(3);
        plain.FindAll(".site-image-grid figcaption").ShouldBeEmpty();
    }

    [Fact]
    public void A_credit_without_its_text_is_refused()
    {
        // Arrange & Act
        var render = () => this.RenderSections(
            [Plain(new() { ImageIds = [Photo] })],
            parameters => parameters.Add(view => view.ImageCredit, _ => new ImageCreditLine(" ")));

        // Assert
        render.ShouldThrow<ArgumentException>().Message.ShouldStartWith("An image credit needs its text.", StringComparison.Ordinal);
    }

    [Fact]
    public void A_section_without_a_heading_sits_on_its_band_without_a_landmark_and_a_plain_tone_is_a_default_band()
    {
        // Arrange
        PageSection<int, string, NoExtra>[] sections =
        [
            Plain(new() { Text = "Viaje diferente!", Tone = SectionTone.Primary, Person = new PagePersonDefinition<int>(Photo, "Ana Exemplo", null) }),
            Plain(new() { Text = "Texto simples." }),
            Plain(new() { ImageIds = [Photo], ImageLayout = SectionImageLayout.BesideText }),
            Plain(new() { Text = "Ao lado da foto.", ImageIds = [Photo], ImageLayout = SectionImageLayout.BesideText }),
        ];

        // Act
        var main = Html(this.RenderSections(sections));

        // Assert
        main.ShouldContain(
            "<div class=\"site-band site-band--default\"><div class=\"site-band__inner\"><div class=\"site-split\"><div class=\"site-split__main\"><p>Ao lado da foto.</p></div>"
            + "<div class=\"site-split__media\"><div class=\"site-image-grid site-image-grid--mosaic\">", StringComparison.Ordinal);
        main.ShouldNotContain("<div class=\"site-split__main\"></div>", StringComparison.Ordinal);
        main.ShouldContain("<div class=\"site-band site-band--primary\"><div class=\"site-band__inner\"><p>Viaje diferente!</p><div class=\"site-byline\">", StringComparison.Ordinal);
        main.ShouldContain("<p class=\"site-byline__name\">Ana Exemplo</p></div></div></div>", StringComparison.Ordinal);
        main.ShouldContain("<div class=\"site-band site-band--default\"><div class=\"site-band__inner\"><p>Texto simples.</p></div></div>", StringComparison.Ordinal);
        main.ShouldContain("<div class=\"site-band site-band--default\"><div class=\"site-band__inner\"><div class=\"site-image-grid site-image-grid--mosaic\">", StringComparison.Ordinal);
        main.ShouldNotContain("aria-labelledby", StringComparison.Ordinal);
        main.ShouldNotContain("<section", StringComparison.Ordinal);
    }

    [Fact]
    public void A_section_with_nothing_left_to_show_renders_nothing()
    {
        // Arrange
        PageSection<int, string, NoExtra>[] sections =
        [
            Plain(new() { Text = "Texto." }),
            Plain(new() { Heading = "Viagem perdida", Link = new PageLinkDefinition<string>("Viagem perdida", LostTarget) }),
            Plain(new() { ImageIds = [Missing], Video = new PageVideoDefinition<int>("https://www.youtube.com/watch?v=abc123", Missing, "Sem pôster") }),
        ];

        // Act
        var main = Html(this.RenderSections(sections));

        // Assert
        main.ShouldContain("<div class=\"site-band site-band--default\"><div class=\"site-band__inner\"><p>Texto.</p></div></div>", StringComparison.Ordinal);
        main.Split("<div class=\"site-band site-band--", StringSplitOptions.None).Length.ShouldBe(2);
        main.ShouldNotContain("Viagem perdida", StringComparison.Ordinal);
        main.ShouldNotContain("Sem pôster", StringComparison.Ordinal);
        main.ShouldNotContain("<div class=\"site-band__inner\"></div>", StringComparison.Ordinal);
    }

    [Fact]
    public void Sections_in_another_language_than_the_page_carry_it_and_the_video_hint_is_in_that_language()
    {
        // Arrange
        var language = ContentCulture.Create("pt-BR").Value;

        // Act
        var main = Html(this.RenderSections([Rich()], parameters => parameters.Add(view => view.TextLanguage, language)));

        // Assert
        main.ShouldStartWith("<div lang=\"pt-BR\"><div class=\"site-band site-band--image\">", StringComparison.Ordinal);
        main.ShouldContain("<span class=\"site-video-link__title\">Nossa viagem</span><span class=\"site-visually-hidden\"> (abre em youtube.com)</span>", StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_text_language_the_sections_carry_none()
    {
        // Arrange & Act
        var main = Html(this.RenderSections([Plain(new() { Text = "Texto." })]));

        // Assert
        main.ShouldStartWith("<div><div class=\"site-band", StringComparison.Ordinal);
    }

    [Fact]
    public void An_image_band_whose_background_is_missing_becomes_a_primary_band_and_a_missing_photo_leaves_the_byline_without_one()
    {
        // Arrange
        var section = Plain(new()
        {
            Text = "Texto.",
            Tone = SectionTone.Image,
            BackgroundImageId = Missing,
            Person = new PagePersonDefinition<int>(Missing, "Ana Exemplo", "Fundadora"),
            ImageIds = [Missing, 2],
        });

        // Act
        var cut = this.RenderSections([section]);

        // Assert
        cut.Find(".site-band").ClassName.ShouldBe("site-band site-band--primary");
        cut.FindAll(".site-band__media").ShouldBeEmpty();
        cut.FindAll(".site-byline__photo").ShouldBeEmpty();
        cut.Find(".site-byline__name").TextContent.ShouldBe("Ana Exemplo");
        cut.FindAll(".site-image-grid img").Select(image => image.GetAttribute("src")).ShouldBe(["/media/2.webp"]);
    }

    [Theory]
    [InlineData(LinkStyle.Primary, "site-button site-button--primary")]
    [InlineData(LinkStyle.Secondary, "site-button site-button--secondary")]
    [InlineData(LinkStyle.Outline, "site-button site-button--outline")]
    public void The_link_style_picks_the_button_variant(LinkStyle style, string classes)
    {
        // Arrange
        var section = Plain(new() { Link = new PageLinkDefinition<string>("Inscreva-se", "contato", style) });

        // Act
        var cut = this.RenderSections([section]);

        // Assert
        var button = cut.Find("p > a");
        button.ClassName.ShouldBe(classes);
        button.GetAttribute("href").ShouldBe("/pt/contato");
        button.TextContent.ShouldBe("Inscreva-se");
    }

    [Fact]
    public void The_extension_renders_with_its_value_between_the_quotes_and_the_video_and_counts_as_something_to_show()
    {
        // Arrange
        PageSection<int, string, TestExtra>[] sections =
        [
            Section(new PageSectionDefinition<int, string, TestExtra>
            {
                Quotes = [new PageQuoteDefinition("Viajar é viver.", null)],
                Extra = new TestExtra(7),
                Video = new PageVideoDefinition<int>("https://youtu.be/abc123", Photo, "Nossa viagem"),
            }),
            Section(new PageSectionDefinition<int, string, TestExtra> { Extra = new TestExtra(8) }),
            Section(new PageSectionDefinition<int, string, TestExtra> { Text = "Sem bloco." }),
        ];

        // Act
        var main = Html(this.RenderSections(sections, parameters => parameters.Add(view => view.Extra, extra => $"<p class=\"block\">Bloco {extra.Block}</p>")));

        // Assert
        var quote = main.IndexOf("<blockquote>Viajar é viver.</blockquote>", StringComparison.Ordinal);
        var block = main.IndexOf("<p class=\"block\">Bloco 7</p>", StringComparison.Ordinal);
        var video = main.IndexOf("<a href=\"https://youtu.be/abc123\"", StringComparison.Ordinal);
        quote.ShouldBeGreaterThan(-1);
        block.ShouldBeGreaterThan(quote);
        video.ShouldBeGreaterThan(block);
        main.ShouldContain("<div class=\"site-band__inner\"><p class=\"block\">Bloco 8</p></div>", StringComparison.Ordinal);
        main.ShouldContain("<div class=\"site-band__inner\"><p>Sem bloco.</p></div>", StringComparison.Ordinal);
        main.Split("class=\"block\"", StringSplitOptions.None).Length.ShouldBe(3);
        main.ShouldContain("(abre em youtu.be)", StringComparison.Ordinal);
    }

    [Fact]
    public void Without_an_extension_template_a_section_of_only_an_extension_renders_nothing()
    {
        // Arrange
        PageSection<int, string, TestExtra>[] sections = [Section(new PageSectionDefinition<int, string, TestExtra> { Extra = new TestExtra(8) })];

        // Act
        var cut = this.RenderSections(sections);

        // Assert
        cut.FindAll(".site-band").ShouldBeEmpty();
    }

    [Fact]
    public void A_hidden_section_is_left_out_and_keeps_its_place_in_the_heading_IDs()
    {
        // Arrange
        PageSection<int, string, TestExtra>[] sections =
        [
            Section(new PageSectionDefinition<int, string, TestExtra> { Heading = "Sob consulta", Text = "Só sob consulta.", Extra = new TestExtra(0, OnRequest: true) }),
            Section(new PageSectionDefinition<int, string, TestExtra> { Heading = "Sempre", Text = "Sempre visível." }),
        ];

        // Act
        var cut = this.RenderSections(sections, parameters => parameters.Add(view => view.IsShown, section => !section.Extra.OnRequest));

        // Assert
        cut.FindAll("section").Count.ShouldBe(1);
        cut.Find("h2").Id.ShouldBe("section-2");
        cut.Markup.ShouldNotContain("Sob consulta", StringComparison.Ordinal);
    }

    [Fact]
    public void The_heading_ID_format_names_the_headings()
    {
        // Arrange & Act
        var cut = this.RenderSections(
            [Plain(new() { Heading = "Quem somos", Text = "Somos uma agência." })],
            parameters => parameters.Add(view => view.HeadingIdFormat, "about-{0}"));

        // Assert
        cut.Find("section").GetAttribute("aria-labelledby").ShouldBe("about-1");
        cut.Find("h2").Id.ShouldBe("about-1");
    }

    [Fact]
    public void The_text_alignment_aligns_every_band_and_the_split_alignment_a_split_without_a_heading()
    {
        // Arrange
        PageSection<int, string, NoExtra>[] sections =
        [
            Plain(new() { Heading = "Quem somos", Text = "Somos uma agência.", ImageIds = [Photo], ImageLayout = SectionImageLayout.BesideText }),
            Plain(new() { Text = "Viaje diferente!", ImageIds = [Photo], ImageLayout = SectionImageLayout.BesideText }),
        ];

        // Act
        var cut = this.RenderSections(sections, parameters => parameters
            .Add(view => view.TextAlignment, SiteAlignment.Center)
            .Add(view => view.SplitAlignment, SiteAlignment.Center));

        // Assert
        cut.FindAll(".site-band").Select(band => band.ClassName).ShouldBe(
        [
            "site-band site-band--default site-band--text-center",
            "site-band site-band--default site-band--text-center",
        ]);
        cut.FindAll(".site-split").Select(split => split.ClassName).ShouldBe(["site-split", "site-split site-split--align-center"]);
    }

    [Fact]
    public void By_default_bands_inherit_their_alignment_and_a_split_starts_at_the_top()
    {
        // Arrange & Act
        var cut = this.RenderSections([Plain(new() { Text = "Viaje diferente!", ImageIds = [Photo], ImageLayout = SectionImageLayout.BesideText })]);

        // Assert
        cut.Find(".site-band").ClassName.ShouldBe("site-band site-band--default");
        cut.Find(".site-split").ClassName.ShouldBe("site-split");
    }

    [Fact]
    public void No_sections_render_an_empty_container()
    {
        // Arrange & Act
        var cut = this.RenderSections<NoExtra>([]);

        // Assert
        Html(cut).ShouldBe("<div></div>");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("section {0}")]
    [InlineData("intro")]
    [InlineData("section-{{0}}")]
    public void A_heading_ID_format_that_is_blank_gives_white_space_or_one_ID_for_every_section_is_refused(string format)
    {
        // Arrange & Act
        var render = () => this.RenderSections([Rich()], parameters => parameters.Add(view => view.HeadingIdFormat, format));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(PageSectionsView<int, string, NoExtra>.HeadingIdFormat));
    }

    [Theory]
    [InlineData("section-{1}")]
    [InlineData("section-{0")]
    public void A_heading_ID_format_that_is_not_a_composite_format_is_an_argument_error_rather_than_a_format_error(string format)
    {
        // Arrange & Act
        var render = () => this.RenderSections([Rich()], parameters => parameters.Add(view => view.HeadingIdFormat, format));

        // Assert
        var exception = render.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe(nameof(PageSectionsView<int, string, NoExtra>.HeadingIdFormat));
        exception.InnerException.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public void A_missing_heading_ID_format_is_a_null_argument()
    {
        // Arrange & Act
        var render = () => this.RenderSections([Rich()], parameters => parameters.Add(view => view.HeadingIdFormat, null!));

        // Assert
        render.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(PageSectionsView<int, string, NoExtra>.HeadingIdFormat));
    }

    [Fact]
    public void A_video_hint_format_that_is_not_a_composite_format_is_an_argument_error_rather_than_a_format_error()
    {
        // Arrange & Act
        var render = () => Render<PageSectionsView<int, string, NoExtra>>(parameters => parameters
            .Add(view => view.Sections, [Rich()])
            .Add(view => view.Image, Image)
            .Add(view => view.Link, Link)
            .Add(view => view.VideoSiteHintFormat, "(opens {1})"));

        // Assert
        var exception = render.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe(nameof(PageSectionsView<int, string, NoExtra>.VideoSiteHintFormat));
        exception.InnerException.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public void A_video_whose_stored_address_is_no_valid_link_is_left_out()
    {
        // Arrange
        PageSection<int, string, NoExtra>[] sections =
        [
            Plain(new() { Video = new PageVideoDefinition<int>("https://www.youtube.com/watch?v=a b", Photo, "Só o vídeo") }),
            Plain(new() { Text = "Texto.", Video = new PageVideoDefinition<int>("https://www.youtube.com/watch?v=a b", Photo, "Vídeo quebrado") }),
        ];

        // Act
        var main = Html(this.RenderSections(sections));

        // Assert
        main.ShouldBe("<div><div class=\"site-band site-band--default\"><div class=\"site-band__inner\"><p>Texto.</p></div></div></div>");
    }

    [Fact]
    public void A_blank_video_hint_format_is_refused()
    {
        // Arrange & Act
        var render = () => Render<PageSectionsView<int, string, NoExtra>>(parameters => parameters
            .Add(view => view.Sections, [Rich()])
            .Add(view => view.Image, Image)
            .Add(view => view.Link, Link)
            .Add(view => view.VideoSiteHintFormat, " "));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(PageSectionsView<int, string, NoExtra>.VideoSiteHintFormat));
    }

    [Fact]
    public void An_undefined_alignment_is_refused()
    {
        // Arrange & Act
        var text = () => this.RenderSections([Rich()], parameters => parameters.Add(view => view.TextAlignment, (SiteAlignment)7));
        var split = () => this.RenderSections([Rich()], parameters => parameters.Add(view => view.SplitAlignment, (SiteAlignment)7));

        // Assert
        text.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(PageSectionsView<int, string, NoExtra>.TextAlignment));
        split.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(PageSectionsView<int, string, NoExtra>.SplitAlignment));
    }

    [Fact]
    public void Missing_sections_or_resolvers_are_refused()
    {
        // Arrange & Act
        var sections = () => Render<PageSectionsView<int, string, NoExtra>>(parameters => parameters
            .Add(view => view.Sections, [null!])
            .Add(view => view.Image, Image)
            .Add(view => view.Link, Link)
            .Add(view => view.VideoSiteHintFormat, HintFormat));
        var image = () => Render<PageSectionsView<int, string, NoExtra>>(parameters => parameters
            .Add(view => view.Sections, [Rich()])
            .Add(view => view.Link, Link)
            .Add(view => view.VideoSiteHintFormat, HintFormat));

        // Assert
        sections.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(PageSectionsView<int, string, NoExtra>.Sections));
        image.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(PageSectionsView<int, string, NoExtra>.Image));
    }
}
