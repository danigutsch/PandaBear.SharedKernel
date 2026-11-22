using static PandaBear.PageSections.Tests.PageSectionFixtures;

namespace PandaBear.PageSections.Tests;

/// <summary>Tests the rules of a translation's list of sections: the count, the heading levels and publishing.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "PageSections")]
public sealed class PageSectionListTests
{
    private readonly SiteContext _context = new();

    [Fact]
    public void More_sections_than_the_maximum_are_refused_on_the_list()
    {
        // Act
        var result = Create(_context, false, [.. Enumerable.Repeat(Text(), 61)]);

        // Assert
        Fields(result.ErrorDetails).ShouldBe([Prefix]);
        result.ErrorDetails.ShouldHaveValidationError(Prefix, "A page cannot have more than 60 sections.");
    }

    [Fact]
    public void A_subsection_follows_a_section_with_a_heading()
    {
        // Arrange
        var subsection = Text("Detalhes") with { Level = SectionLevel.Subsection };

        // Act
        var valid = Create(_context, false, Text(), subsection, subsection);
        var first = Create(_context, false, subsection);
        var afterUnheaded = Create(_context, false, Text(heading: null), subsection);

        // Assert
        valid.ShouldBeSuccess().Select(section => section.Level).ShouldBe([SectionLevel.Section, SectionLevel.Subsection, SectionLevel.Subsection]);
        first.ErrorDetails.ShouldHaveValidationError("pt.sections[0].level", "A subsection must follow a section with a heading.");
        afterUnheaded.ErrorDetails.ShouldHaveValidationError("pt.sections[1].level", "A subsection must follow a section with a heading.");
    }

    [Fact]
    public void Publishing_needs_a_section()
    {
        // Act
        var draft = Create(_context, false);
        var published = Create(_context, true);

        // Assert
        draft.ShouldBeSuccess().ShouldBeEmpty();
        published.ErrorDetails.ShouldHaveValidationError(Prefix, "A page needs at least one section before publishing.");
    }

    [Fact]
    public void Publishing_needs_every_image_shown_eligible_in_the_culture()
    {
        // Arrange
        var both = _context.Add(Portuguese, English);
        var englishOnly = _context.Add(English);
        var section = Text() with { Person = new(englishOnly, "Ana", null), ImageIds = [both, englishOnly] };

        // Act
        var published = Create(_context, true, section);
        var draft = Create(_context, false, section);
        var ineligible = PageSectionList.FindIneligible(draft.ShouldBeSuccess(), Portuguese, _context);

        // Assert
        Fields(published.ErrorDetails).ShouldBe([PageSectionFields.Images(Portuguese)]);
        published.ErrorDetails.ShouldHaveValidationError("pt.images", "Every image must be eligible in this language before publishing; 1 are not.");
        ineligible.ShouldBe([englishOnly]);
    }

    [Fact]
    public void Eligibility_is_checked_only_once_the_sections_are_otherwise_valid()
    {
        // Arrange
        var englishOnly = _context.Add(English);

        // Act
        var result = Create(_context, true, Text(heading: new string('h', 201)) with { ImageIds = [englishOnly] });

        // Assert
        Fields(result.ErrorDetails).ShouldBe(["pt.sections[0].heading"]);
    }

    [Fact]
    public void Errors_are_placed_under_the_given_prefix()
    {
        // Act
        var result = PageSectionList.Create([Text(), Text(heading: null, text: null)], Rules, _context, English, published: false, "en.sections");

        // Assert
        Fields(result.ErrorDetails).ShouldBe(["en.sections[1]"]);
    }

    [Fact]
    public void Every_image_shown_by_the_sections_is_listed()
    {
        // Arrange
        var first = _context.Add(Portuguese);
        var second = _context.Add(Portuguese);
        var sections = Create(_context, false, Text() with { ImageIds = [first] }, Text() with { ImageIds = [second, first] }).ShouldBeSuccess();

        // Act
        var shown = PageSectionList.ImagesShown(sections);

        // Assert
        shown.ShouldBe([first, second]);
    }

    [Fact]
    public void Missing_arguments_are_refused()
    {
        // Act
        Action noRules = () => PageSectionList.Create([], null!, _context, Portuguese, false, Prefix);
        Action noContext = () => PageSectionList.Create<Guid, NavLink, SiteExtra>([], Rules, null!, Portuguese, false, Prefix);
        Action noCulture = () => PageSectionList.Create([], Rules, _context, null!, false, Prefix);
        Action noPrefix = () => PageSectionList.Create([], Rules, _context, Portuguese, false, null!);
        Action nullSection = () => PageSectionList.Create([null!], Rules, _context, Portuguese, false, Prefix);
        Action noSections = () => PageSectionList.ImagesShown<Guid, NavLink, SiteExtra>(null!);

        // Assert
        noRules.ShouldThrow<ArgumentNullException>();
        noContext.ShouldThrow<ArgumentNullException>();
        noCulture.ShouldThrow<ArgumentNullException>();
        noPrefix.ShouldThrow<ArgumentNullException>();
        nullSection.ShouldThrow<ArgumentNullException>();
        noSections.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void A_missing_list_has_no_sections()
    {
        // Act
        var result = PageSectionList.Create(null, Rules, _context, Portuguese, false, Prefix);

        // Assert
        result.ShouldBeSuccess().ShouldBeEmpty();
    }

    [Fact]
    public void An_application_without_an_extension_uses_no_extra()
    {
        // Arrange
        var context = new PlainContext();

        // Act
        var result = PageSectionList.Create([new PageSectionDefinition<Guid, string, NoExtra> { Text = "Texto.", Link = new("Ver", "/sobre") }], Rules, context, Portuguese, false, Prefix);
        var empty = PageSectionList.Create([new PageSectionDefinition<Guid, string, NoExtra> { Heading = "Só título" }], Rules, context, Portuguese, false, Prefix);

        // Assert
        var section = result.ShouldBeSuccess().ShouldHaveSingleItem();
        section.Extra.ShouldBe(default);
        section.Link.ShouldNotBeNull().Target.ShouldBe("/sobre");
        empty.ErrorDetails.ShouldHaveValidationError("pt.sections[0]", "A section needs text, questions, a person, a quote, images, a video, a button or other content.");
    }

    [Fact]
    public void Rules_have_v2s_defaults_and_refuse_a_non_positive_limit()
    {
        // Act
        var rules = new PageSectionRules();
        Action zero = () => _ = new PageSectionRules { MaxQuestions = 0 };

        // Assert
        (rules.MaxSections, rules.MaxQuestions, rules.MaxQuotes, rules.MaxImages).ShouldBe((60, 30, 60, 60));
        (rules.MaxParagraphs, rules.MaxParagraphLength).ShouldBe((50, 4000));
        (rules.MaxTitleLength, rules.MaxLineLength, rules.MaxQuoteLength).ShouldBe((200, 300, 1000));
        zero.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Field_paths_are_v2s()
    {
        // Act & Assert
        PageSectionFields.Section("pt.sections", 2).ShouldBe("pt.sections[2]");
        PageSectionFields.Images(ContentCulture.Restore("pt-BR")).ShouldBe("pt-BR.images");
        string[] parts =
        [
            PageSectionFields.Eyebrow, PageSectionFields.Heading, PageSectionFields.Level, PageSectionFields.Text, PageSectionFields.Questions,
            PageSectionFields.Question, PageSectionFields.Answer, PageSectionFields.Tone, PageSectionFields.BackgroundImageId, PageSectionFields.Person,
            PageSectionFields.Name, PageSectionFields.Role, PageSectionFields.ImageId, PageSectionFields.Quotes, PageSectionFields.QuoteText,
            PageSectionFields.Cite, PageSectionFields.ImageIds, PageSectionFields.ImageLayout, PageSectionFields.Video, PageSectionFields.Address,
            PageSectionFields.PosterImageId, PageSectionFields.Title, PageSectionFields.Link, PageSectionFields.LinkText, PageSectionFields.Style,
        ];
        parts.ShouldBe(
        [
            "eyebrow", "heading", "level", "text", "questions", "question", "answer", "tone", "backgroundImageId", "person", "name", "role", "imageId",
            "quotes", "text", "cite", "imageIds", "imageLayout", "video", "address", "posterImageId", "title", "link", "text", "style",
        ]);
    }
}
