using static SharedKernel.PageSections.Tests.PageSectionFixtures;

namespace SharedKernel.PageSections.Tests;

/// <summary>Tests storing a section as a definition and restoring it.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "PageSections")]
public sealed class PageSectionRestoreTests
{
    private readonly SiteContext _context = new();

    [Fact]
    public void A_section_round_trips_through_its_definition()
    {
        // Arrange
        var image = _context.Add(Portuguese);
        var section = Valid(_context, new SiteSectionDefinition
        {
            Eyebrow = "Desde 1998",
            Heading = "Quem somos",
            Level = SectionLevel.Section,
            Text = "Um.\n\nDois.",
            Questions = [new("Q?", "A.\n\nB.")],
            Person = new(image, "Ana", null),
            Quotes = [new("Q", "C")],
            Extra = new SiteExtra(PageBlock.ContactDetails, SectionCondition.Always),
            Tone = SectionTone.Image,
            BackgroundImageId = image,
            ImageIds = [image],
            ImageLayout = SectionImageLayout.BesideText,
            Video = new("https://example.org/v", image, "Filme"),
            Link = new("Ver", new NavLink("about", null), LinkStyle.Secondary),
        });

        // Act
        var definition = section.ToDefinition(route => route.ToInput());
        var restored = PageSection.Restore(definition, NavRoute.Restore);
        var created = Valid(_context, definition);

        // Assert
        definition.Text.ShouldBe("Um.\n\nDois.");
        definition.Questions.ShouldNotBeNull().ShouldHaveSingleItem().ShouldBe(new PageQuestionDefinition("Q?", "A.\n\nB."));
        definition.Video.ShouldBe(new PageVideoDefinition<Guid>("https://example.org/v", image, "Filme"));
        restored.ShouldBe(section);
        created.ShouldBe(section);
    }

    [Fact]
    public void Restoring_runs_no_limits_or_publish_rules()
    {
        // Act
        var section = PageSection.Restore(new SiteSectionDefinition { Heading = new string('h', 500), Tone = SectionTone.Image }, NavRoute.Restore);

        // Assert
        section.Heading.ShouldNotBeNull().Length.ShouldBe(500);
        section.BackgroundImageId.ShouldBeNull();
    }

    [Fact]
    public void Restoring_refuses_what_rendering_cannot_handle()
    {
        // Act & Assert
        ShouldNotRestore(Text() with { Tone = (SectionTone)9 });
        ShouldNotRestore(Text() with { Level = (SectionLevel)9 });
        ShouldNotRestore(Text() with { ImageLayout = (SectionImageLayout)9 });
        ShouldNotRestore(Text() with { Link = new("Ver", new NavLink("about", null), (LinkStyle)9) });
        ShouldNotRestore(Text() with { Link = new(" ", new NavLink("about", null)) });
        ShouldNotRestore(Text() with { Link = new("Ver", null) });
        ShouldNotRestore(Text() with { Person = new(null, null, null) });
        ShouldNotRestore(Text() with { Quotes = [new(" ", null)] });
        ShouldNotRestore(Text() with { Questions = [new("Q?", null)] });
        ShouldNotRestore(Text() with { Video = new("https://example.org/v", null, "Filme") });
        ShouldNotRestore(Text() with { Video = new(" ", Guid.CreateVersion7(), "Filme") });
        ShouldNotRestore(Text(text: "Broken \uD800"));
    }

    [Fact]
    public void Restoring_refuses_a_missing_definition_or_entry()
    {
        // Act
        Action noDefinition = () => PageSection.Restore<Guid, NavLink, NavRoute, SiteExtra>(null!, NavRoute.Restore);
        Action noRestoreLink = () => PageSection.Restore<Guid, NavLink, NavRoute, SiteExtra>(Text(), null!);
        Action nullQuote = () => PageSection.Restore(Text() with { Quotes = [null!] }, NavRoute.Restore);
        Action noLinkInput = () => Valid(_context, Text()).ToDefinition<NavLink>(null!);

        // Assert
        noDefinition.ShouldThrow<ArgumentNullException>();
        noRestoreLink.ShouldThrow<ArgumentNullException>();
        noLinkInput.ShouldThrow<ArgumentNullException>();
        nullQuote.ShouldThrow<ArgumentNullException>();
    }
}
