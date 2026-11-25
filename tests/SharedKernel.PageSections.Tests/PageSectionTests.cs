using static SharedKernel.PageSections.Tests.PageSectionFixtures;

namespace SharedKernel.PageSections.Tests;

/// <summary>Tests the parts of one page section and their rules, ported from ViajantesTurismo V2.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "PageSections")]
public sealed class PageSectionTests
{
    private const string Field = "pt.sections[0]";

    private readonly SiteContext _context = new();

    [Fact]
    public void A_section_keeps_every_part_and_lists_the_images_it_shows()
    {
        // Arrange
        var background = _context.Add(Portuguese);
        var photo = _context.Add(Portuguese);
        var first = _context.Add(Portuguese);
        var second = _context.Add(Portuguese);
        var poster = _context.Add(Portuguese);
        var definition = new SiteSectionDefinition
        {
            Eyebrow = " Desde 1998 ",
            Heading = "Quem somos",
            Text = "Primeiro.\n\nSegundo\nainda.",
            Questions = [new("Como reservo?", "Pelo formulário.\n\nOu por telefone.")],
            Person = new(photo, "Ana", "Guia"),
            Quotes = [new("Inesquecível.", "Maria")],
            Extra = new SiteExtra(PageBlock.ContactDetails, SectionCondition.Always),
            Tone = SectionTone.Image,
            BackgroundImageId = background,
            ImageIds = [first, second],
            ImageLayout = SectionImageLayout.BesideText,
            Video = new("https://www.youtube.com/watch?v=abc", poster, "Nosso filme"),
            Link = new("Fale conosco", new NavLink("contact", null), LinkStyle.Outline),
        };

        // Act
        var section = Valid(_context, definition);

        // Assert
        section.Eyebrow.ShouldBe("Desde 1998");
        section.Heading.ShouldBe("Quem somos");
        section.Level.ShouldBe(SectionLevel.Section);
        section.Paragraphs.ShouldBe(["Primeiro.", "Segundo ainda."]);
        var question = section.Questions.ShouldHaveSingleItem();
        question.Question.ShouldBe("Como reservo?");
        question.Answer.ShouldBe(["Pelo formulário.", "Ou por telefone."]);
        section.Person.ShouldNotBeNull().Name.ShouldBe("Ana");
        section.Person.Role.ShouldBe("Guia");
        section.Person.ImageId.ShouldBe(photo);
        section.Quotes.ShouldHaveSingleItem().Cite.ShouldBe("Maria");
        section.Extra.ShouldBe(new SiteExtra(PageBlock.ContactDetails, SectionCondition.Always));
        section.Tone.ShouldBe(SectionTone.Image);
        section.BackgroundImageId.ShouldBe(background);
        section.ImageIds.ShouldBe([first, second]);
        section.ImageLayout.ShouldBe(SectionImageLayout.BesideText);
        section.Video.ShouldNotBeNull().Address.ShouldBe(WebUrl.Restore("https://www.youtube.com/watch?v=abc"));
        section.Video.PosterImageId.ShouldBe(poster);
        section.Video.Title.ShouldBe("Nosso filme");
        section.Link.ShouldNotBeNull().Text.ShouldBe("Fale conosco");
        section.Link.Target.PageKey.ShouldBe("contact");
        section.Link.Style.ShouldBe(LinkStyle.Outline);
        section.ImagesShown().ShouldBe([background, photo, first, second, poster]);
    }

    [Fact]
    public void A_button_is_primary_by_default_and_takes_the_target_the_context_normalized()
    {
        // Act
        var section = Valid(_context, new SiteSectionDefinition { Link = new("Ver", new NavLink(null, "HTTPS://Example.org/a")) });

        // Assert
        section.Link.ShouldNotBeNull().Style.ShouldBe(LinkStyle.Primary);
        section.Link.Target.ExternalLink.ShouldBe("https://example.org/a");
        section.Heading.ShouldBeNull();
        section.Tone.ShouldBe(SectionTone.Plain);
    }

    [Fact]
    public void Text_limits_are_v2s()
    {
        // Act
        var error = Refused(_context, new SiteSectionDefinition
        {
            Eyebrow = new string('e', 301),
            Heading = new string('h', 201),
            Text = string.Join("\n\n", Enumerable.Repeat("p", 51)),
        });
        var paragraph = Refused(_context, Text(text: new string('x', 4001)));
        var malformed = Refused(_context, Text(heading: "Broken \uD800"));

        // Assert
        error.ShouldHaveValidationError(Field + ".eyebrow", "Section eyebrow cannot exceed 300 characters.");
        error.ShouldHaveValidationError(Field + ".heading", "Section heading cannot exceed 200 characters.");
        error.ShouldHaveValidationError(Field + ".text", "Section text cannot have more than 50 paragraphs.");
        paragraph.ShouldHaveValidationError(Field + ".text", "Section text: each paragraph must be at most 4000 characters.");
        malformed.ShouldHaveValidationError(Field + ".heading", "Section heading is not valid text.");
    }

    [Fact]
    public void A_subsection_needs_a_heading_and_a_level_must_be_defined()
    {
        // Act
        var noHeading = Create(_context, false, Text(), Text(heading: " ") with { Level = SectionLevel.Subsection });
        var undefined = Refused(_context, Text() with { Level = (SectionLevel)5 });

        // Assert
        noHeading.ErrorDetails.ShouldHaveValidationError("pt.sections[1].heading", "A subsection needs a heading.");
        undefined.ShouldHaveValidationError(Field + ".level", "The heading level is not supported.");
    }

    [Fact]
    public void Over_long_text_reports_only_its_cap_and_still_counts_as_given()
    {
        // Act
        var heading = Create(_context, false, Text(), Text(heading: new string('h', 201)) with { Level = SectionLevel.Subsection });
        var text = Refused(_context, Text(heading: null, text: new string('x', 4001)));
        var answer = Refused(_context, Text() with { Questions = [new("Q?", new string('a', 4001))] });

        // Assert
        Fields(heading.ErrorDetails).ShouldBe(["pt.sections[1].heading"]);
        heading.ErrorDetails.ShouldHaveValidationError("pt.sections[1].heading", "Section heading cannot exceed 200 characters.");
        Fields(text).ShouldBe([Field + ".text"]);
        text.ShouldHaveValidationError(Field + ".text", "Section text: each paragraph must be at most 4000 characters.");
        Fields(answer).ShouldBe([Field + ".questions[0].answer"]);
        answer.ShouldHaveValidationError(Field + ".questions[0].answer", "Question 1 answer: each paragraph must be at most 4000 characters.");
    }

    [Fact]
    public void Malformed_text_reports_only_that_it_is_not_valid_text()
    {
        // Act
        var heading = Create(_context, false, Text(), Text(heading: "Broken \uD800") with { Level = SectionLevel.Subsection });
        var text = Refused(_context, Text(heading: null, text: "Broken \uD800"));

        // Assert
        Fields(heading.ErrorDetails).ShouldBe(["pt.sections[1].heading"]);
        heading.ErrorDetails.ShouldHaveValidationError("pt.sections[1].heading", "Section heading is not valid text.");
        Fields(text).ShouldBe([Field + ".text"]);
        text.ShouldHaveValidationError(Field + ".text", "Section text is not valid text.");
    }

    [Fact]
    public void Questions_are_bounded_and_each_needs_a_question_and_an_answer()
    {
        // Act
        var tooMany = Refused(_context, Text() with { Questions = [.. Enumerable.Repeat(new PageQuestionDefinition("Q?", "A."), 31)] });
        var parts = Refused(_context, Text() with { Questions = [new("Q?", "A."), new(" ", null), new(new string('q', 301), "A.")] });

        // Assert
        tooMany.ShouldHaveValidationError(Field + ".questions", "A section cannot have more than 30 questions.");
        parts.ShouldHaveValidationError(Field + ".questions[1].question", "Question 2 is required.");
        parts.ShouldHaveValidationError(Field + ".questions[1].answer", "Question 2 answer is required.");
        parts.ShouldHaveValidationError(Field + ".questions[2].question", "Question 3 cannot exceed 300 characters.");
    }

    [Fact]
    public void Tone_and_background_agree_and_the_background_exists()
    {
        // Arrange
        var image = _context.Add(Portuguese);

        // Act
        var undefined = Refused(_context, Text() with { Tone = (SectionTone)9 });
        var missing = Refused(_context, Text() with { Tone = SectionTone.Image });
        var notImage = Refused(_context, Text() with { Tone = SectionTone.Muted, BackgroundImageId = image });
        var unknown = Refused(_context, Text() with { Tone = SectionTone.Image, BackgroundImageId = Guid.CreateVersion7() });

        // Assert
        undefined.ShouldHaveValidationError(Field + ".tone", "The section tone is not supported.");
        missing.ShouldHaveValidationError(Field + ".backgroundImageId", "A section on an image band needs a background image.");
        notImage.ShouldHaveValidationError(Field + ".backgroundImageId", "Only a section on an image band has a background image.");
        unknown.ShouldHaveValidationError(Field + ".backgroundImageId", "The background image does not exist.");
    }

    [Fact]
    public void A_person_needs_a_name_and_an_existing_photo()
    {
        // Act
        var error = Refused(_context, Text() with { Person = new(Guid.CreateVersion7(), " ", new string('r', 201)) });

        // Assert
        error.ShouldHaveValidationError(Field + ".person.name", "Person name is required.");
        error.ShouldHaveValidationError(Field + ".person.role", "Person role cannot exceed 200 characters.");
        error.ShouldHaveValidationError(Field + ".person.imageId", "The person's photo does not exist.");
    }

    [Fact]
    public void Quotes_are_bounded_and_each_needs_its_words()
    {
        // Act
        var tooMany = Refused(_context, Text() with { Quotes = [.. Enumerable.Repeat(new PageQuoteDefinition("Q", null), 61)] });
        var parts = Refused(_context, Text() with { Quotes = [new(null, null), new(new string('t', 1001), new string('c', 201))] });

        // Assert
        tooMany.ShouldHaveValidationError(Field + ".quotes", "A section cannot have more than 60 quotes.");
        parts.ShouldHaveValidationError(Field + ".quotes[0].text", "Quote 1 text is required.");
        parts.ShouldHaveValidationError(Field + ".quotes[1].text", "Quote 2 text cannot exceed 1000 characters.");
        parts.ShouldHaveValidationError(Field + ".quotes[1].cite", "Quote 2 source cannot exceed 200 characters.");
    }

    [Fact]
    public void Images_exist_appear_once_and_are_bounded_and_the_layout_is_defined()
    {
        // Arrange
        var image = _context.Add(Portuguese);
        Guid[] many = [.. Enumerable.Range(0, 61).Select(_ => _context.Add(Portuguese))];

        // Act
        var tooMany = Refused(_context, Text() with { ImageIds = many });
        var missing = Refused(_context, Text() with { ImageIds = [image, Guid.CreateVersion7()] });
        var twice = Refused(_context, Text() with { ImageIds = [image, image] });
        var layout = Refused(_context, Text() with { ImageLayout = (SectionImageLayout)4 });

        // Assert
        tooMany.ShouldHaveValidationError(Field + ".imageIds", "A section cannot show more than 60 images.");
        missing.ShouldHaveValidationError(Field + ".imageIds", "An image of the section does not exist.");
        twice.ShouldHaveValidationError(Field + ".imageIds", "An image of the section appears twice.");
        layout.ShouldHaveValidationError(Field + ".imageLayout", "The image layout is not supported.");
    }

    [Fact]
    public void A_video_needs_an_https_address_an_existing_poster_and_a_title()
    {
        // Act
        var empty = Refused(_context, Text() with { Video = new("http://example.org/v", null, null) });
        var emptyId = Refused(_context, Text() with { Video = new("https://example.org/v", Guid.Empty, "Filme") });
        var unknown = Refused(_context, Text() with { Video = new("https://example.org/v", Guid.CreateVersion7(), "Filme") });

        // Assert
        Fields(empty).ShouldBe([Field + ".video.address", Field + ".video.posterImageId", Field + ".video.title"]);
        empty.ShouldHaveValidationError(Field + ".video.posterImageId", "Choose a poster image for the video.");
        empty.ShouldHaveValidationError(Field + ".video.title", "Video title is required.");
        emptyId.ShouldHaveValidationError(Field + ".video.posterImageId", "Choose a poster image for the video.");
        unknown.ShouldHaveValidationError(Field + ".video.posterImageId", "The poster image does not exist.");
    }

    [Fact]
    public void A_button_needs_text_a_defined_style_and_a_target_the_context_accepts()
    {
        // Act
        var error = Refused(_context, Text() with { Link = new(" ", new NavLink("unknown", null), (LinkStyle)7) });
        var external = Refused(_context, Text() with { Link = new("Ver", new NavLink(null, "http://example.org")) });

        // Assert
        error.ShouldHaveValidationError(Field + ".link.text", "Button text is required.");
        error.ShouldHaveValidationError(Field + ".link.style", "The button style is not supported.");
        error.ShouldHaveValidationError(Field + ".link.pageKey", "The page does not exist.");
        Fields(external).ShouldBe([Field + ".link.externalLink"]);
    }

    [Fact]
    public void A_button_without_a_target_is_a_validation_error_on_the_button()
    {
        // Act
        var error = Refused(_context, new SiteSectionDefinition { Link = new("Ver", null) });

        // Assert
        Fields(error).ShouldBe([Field + ".link"]);
        error.ShouldHaveValidationError(Field + ".link", "Choose where the button links.");
    }

    [Fact]
    public void A_section_whose_only_content_is_an_invalid_button_reports_the_button()
    {
        // Act
        var noText = Refused(_context, new SiteSectionDefinition { Link = new(" ", new NavLink("about", null)) });
        var badTarget = Refused(_context, new SiteSectionDefinition { Link = new("Ver", new NavLink("unknown", null)) });

        // Assert
        Fields(noText).ShouldBe([Field + ".link.text"]);
        Fields(badTarget).ShouldBe([Field + ".link.pageKey"]);
    }

    [Fact]
    public void A_section_must_show_more_than_a_heading()
    {
        // Act
        var error = Refused(_context, new SiteSectionDefinition { Eyebrow = "Desde 1998", Heading = "Quem somos", Extra = new SiteExtra(PageBlock.None, SectionCondition.Always) });

        // Assert
        error.ShouldHaveValidationError(Field, "A section needs text, questions, a person, a quote, images, a video, a button or other content.");
    }

    [Fact]
    public void Each_part_counts_as_content()
    {
        // Arrange
        var image = _context.Add(Portuguese);
        SiteSectionDefinition[] sections =
        [
            new() { Text = "Texto." },
            new() { Questions = [new("Q?", "A.")] },
            new() { Person = new(null, "Ana", null) },
            new() { Quotes = [new("Q", null)] },
            new() { ImageIds = [image] },
            new() { Video = new("https://example.org/v", image, "Filme") },
            new() { Link = new("Ver", new NavLink("about", null)) },
            new() { Extra = new SiteExtra(PageBlock.ContactDetails, SectionCondition.Always) },
        ];

        // Act
        var result = Create(_context, false, sections);

        // Assert
        result.ShouldBeSuccess().Count.ShouldBe(8);
    }

    [Fact]
    public void The_context_validates_the_extension_with_the_publication_status()
    {
        // Arrange
        var bank = Text() with { Extra = new SiteExtra(PageBlock.BankDetails, SectionCondition.Always) };
        var conditional = Text() with { Extra = new SiteExtra(PageBlock.None, SectionCondition.OnRequest) };

        // Act
        var draft = Create(_context, false, bank);
        var published = Create(_context, true, bank);
        var offPage = Create(_context, false, conditional);
        _context.TourInformation = true;
        var onPage = Create(_context, false, conditional);

        // Assert
        draft.ShouldBeSuccess();
        published.ErrorDetails.ShouldHaveValidationError(Field + ".block", "The bank details block needs the bank settings before publishing.");
        offPage.ErrorDetails.ShouldHaveValidationError(Field + ".condition", "Conditions are allowed only on the tour information page.");
        onPage.ShouldBeSuccess().ShouldHaveSingleItem().Extra.Condition.ShouldBe(SectionCondition.OnRequest);
    }

    [Fact]
    public void Sections_with_the_same_data_are_equal()
    {
        // Arrange
        var image = _context.Add(Portuguese);
        var definition = Text() with { Questions = [new("Q?", "A.")], ImageIds = [image], Quotes = [new("Q", "C")], Link = new("Ver", new NavLink("about", null)) };

        // Act
        var first = Valid(_context, definition);
        var second = Valid(_context, definition);
        var other = Valid(_context, definition with { Questions = [new("Q?", "B.")] });

        // Assert
        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.Equals(other).ShouldBeFalse();
        first.Questions[0].Equals(second.Questions[0]).ShouldBeTrue();
        first.Questions[0].GetHashCode().ShouldBe(second.Questions[0].GetHashCode());
        first.Equals((object?)null).ShouldBeFalse();
    }
}
