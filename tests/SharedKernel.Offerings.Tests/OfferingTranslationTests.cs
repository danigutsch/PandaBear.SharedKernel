using static PandaBear.Offerings.Tests.OfferingFixtures;

namespace PandaBear.Offerings.Tests;

/// <summary>Tests the text of an offering per culture and the publish rule of a translation.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Offerings")]
public sealed class OfferingTranslationTests
{
    [Fact]
    public void A_translation_keeps_its_sanitized_text_and_derives_a_blank_slug_from_the_title()
    {
        // Act
        var offering = Valid(Tour(Translation(status: PublicationStatus.Published) with
        {
            Title = "  Rota   Romântica ",
            Subtitle = "Castelos e vinhos",
            SeoTitle = "Rota Romântica de bicicleta",
            SeoDescription = "Oito dias pelo sul da Alemanha.",
            Description = "Um\r\ndois\n\n\nTrês",
            NotIncluded = ["Voos"],
            PracticalInfo = ["Chegada individual"],
        }));

        // Assert
        var translation = offering.Translations.ShouldHaveSingleItem();
        translation.Culture.ShouldBe(Portuguese);
        translation.Status.ShouldBe(PublicationStatus.Published);
        translation.Title.ShouldBe("Rota Romântica");
        translation.Slug.Value.ShouldBe("rota-romantica");
        translation.Subtitle.ShouldBe("Castelos e vinhos");
        translation.Eyebrow.ShouldBe("Alemanha");
        translation.Summary.ShouldBe("Castelos e vinhedos de bicicleta.");
        translation.Description.ShouldBe(["Um dois", "Três"]);
        translation.SeoTitle.ShouldBe("Rota Romântica de bicicleta");
        translation.SeoDescription.ShouldBe("Oito dias pelo sul da Alemanha.");
        translation.Highlights.ShouldBe(["Würzburg", "Rothenburg", "Füssen"]);
        translation.Included.ShouldBe(["Hotel", "Bike"]);
        translation.NotIncluded.ShouldBe(["Voos"]);
        translation.PracticalInfo.ShouldBe(["Chegada individual"]);
        translation.DateNote.ShouldBeNull();
        offering.IsPublishedIn(Portuguese).ShouldBeTrue();
        offering.IsPublishedIn(English).ShouldBeFalse();
    }

    [Fact]
    public void Translations_use_the_rules_cultures_once_each_and_include_the_default_culture()
    {
        // Act
        var unknown = Refused(Tour(Translation(), Translation("fr")));
        var twice = Refused(Tour(Translation(), Translation("PT")));
        var noDefault = Refused(Tour(Translation("en")));

        // Assert
        unknown.ShouldHaveValidationError(OfferingFields.Translations, "Each translation must use one of the offering languages: pt, en.");
        twice.ShouldHaveValidationError(OfferingFields.Translations, "Each language can have only one translation.");
        noDefault.ShouldHaveValidationError(OfferingFields.Translations, "A translation in the default language (pt) is required.");
    }

    [Theory]
    [InlineData(null, "Title is required.")]
    [InlineData("  ", "Title is required.")]
    [InlineData("01234567890123456789012345678901234567890", "Title cannot exceed 40 characters.")]
    public void A_missing_long_or_malformed_title_is_refused(string? title, string message)
    {
        // Act
        var error = Refused(Tour(Translation() with { Title = title, Slug = "rota" }));

        // Assert
        error.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.Title), message);
    }

    [Fact]
    public void A_malformed_title_is_refused_without_an_exception()
    {
        // Act
        var error = Refused(Tour(Translation() with { Title = "Broken \uD800", Slug = "rota" }));

        // Assert
        error.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.Title), "Title is not valid text.");
    }

    [Fact]
    public void A_given_slug_must_be_valid_and_a_title_without_letters_needs_one()
    {
        // Act
        var invalid = Refused(Tour(Translation() with { Slug = "rota/romantica" }));
        var noLetters = Refused(Tour(Translation() with { Title = "東京" }));
        var given = Valid(Tour(Translation() with { Slug = "Rota Romantica 2027" }));

        // Assert
        invalid.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.Slug), "Slug can only contain letters, digits, spaces, '-' and '_'.");
        noLetters.ShouldHaveValidationError("pt.slug", "Slug must contain a letter or a digit.");
        given.Translations[0].Slug.Value.ShouldBe("rota-romantica-2027");
    }

    [Fact]
    public void An_undefined_status_is_refused()
    {
        // Act
        var error = Refused(Tour(Translation() with { Status = (PublicationStatus)9 }));

        // Assert
        error.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.Status), "The publication status is not valid.");
    }

    [Fact]
    public void Long_text_is_refused_on_its_field()
    {
        // Act
        var error = Refused(Tour(Translation() with
        {
            Subtitle = new string('s', 301),
            Eyebrow = new string('e', 301),
            Summary = new string('u', 1001),
            SeoTitle = new string('t', 41),
            SeoDescription = new string('d', 1001),
            Description = string.Join("\n\n", Enumerable.Repeat("p", 51)),
        }));

        // Assert
        error.ShouldHaveValidationError("pt.subtitle", "Subtitle cannot exceed 300 characters.");
        error.ShouldHaveValidationError("pt.eyebrow", "Eyebrow cannot exceed 300 characters.");
        error.ShouldHaveValidationError("pt.summary", "Summary cannot exceed 1000 characters.");
        error.ShouldHaveValidationError("pt.seoTitle", "SEO title cannot exceed 40 characters.");
        error.ShouldHaveValidationError("pt.seoDescription", "SEO description cannot exceed 1000 characters.");
        error.ShouldHaveValidationError("pt.description", "Description cannot have more than 50 paragraphs.");
    }

    [Fact]
    public void Feature_lists_hold_one_line_per_item()
    {
        // Act
        var empty = Refused(Tour(Translation() with { Included = ["Hotel", ""] }));
        var tooMany = Refused(Tour(Translation() with { NotIncluded = [.. Enumerable.Repeat("x", 13)] }));
        var tooLong = Refused(Tour(Translation() with { PracticalInfo = [new string('x', 301)] }));
        var malformed = Refused(Tour(Translation() with { Highlights = ["A", "B", "\uDC00"] }));

        // Assert
        empty.ShouldHaveValidationError("pt.included", "Included cannot have an empty item.");
        tooMany.ShouldHaveValidationError("pt.notIncluded", "Not included cannot have more than 12 items.");
        tooLong.ShouldHaveValidationError("pt.practicalInfo", "Practical information: each item must be at most 300 characters.");
        malformed.ShouldHaveValidationError("pt.highlights", "Highlights is not valid text.");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void Highlights_are_none_or_within_the_configured_range(int count, bool valid)
    {
        // Arrange
        var definition = Tour(Translation() with { Highlights = [.. Enumerable.Range(1, count).Select(index => "H" + index)] });

        // Act
        var result = Offering.Create(definition, Rules, new ImageLibrary());

        // Assert
        if (valid)
        {
            result.ShouldBeSuccess().Translations[0].Highlights.Count.ShouldBe(count);
        }
        else
        {
            result.ErrorDetails.ShouldHaveValidationError("pt.highlights", "Give no highlights, or between 3 and 10.");
        }
    }

    [Fact]
    public void Publishing_needs_a_summary_a_description_and_a_text_for_every_step()
    {
        // Act
        var error = Refused(Tour(Translation(status: PublicationStatus.Published) with { Summary = " ", Description = null, StepTexts = [new("A")] }));

        // Assert
        Fields(error).ShouldBe(["pt.description", "pt.steps", "pt.summary"]);
        error.ShouldHaveValidationError("pt.summary", "A summary is required before publishing.");
        error.ShouldHaveValidationError("pt.description", "A description is required before publishing.");
        error.ShouldHaveValidationError("pt.steps", "Every step needs its text in this language before publishing.");
    }

    [Fact]
    public void An_over_long_summary_or_description_reports_only_its_cap_when_publishing()
    {
        // Arrange
        var summary = new string('s', Rules.MaxSummaryLength + 1);
        var description = new string('d', Rules.MaxParagraphLength + 1);

        // Act
        var error = Refused(Tour(Translation(status: PublicationStatus.Published) with { Summary = summary, Description = description }));

        // Assert
        Fields(error).ShouldBe(["pt.description", "pt.summary"]);
        error.ShouldHaveValidationError("pt.summary", $"Summary cannot exceed {Rules.MaxSummaryLength} characters.");
        error.ShouldHaveValidationError("pt.description", $"Description: each paragraph must be at most {Rules.MaxParagraphLength} characters.");
    }

    [Fact]
    public void A_draft_may_be_incomplete()
    {
        // Act
        var offering = Valid(Tour(Translation() with { Summary = null, Description = null, StepTexts = [] }));

        // Assert
        offering.Translations[0].Summary.ShouldBeNull();
        offering.Translations[0].Description.ShouldBeEmpty();
        offering.Translations[0].StepTexts.ShouldBeEmpty();
    }

    [Fact]
    public void Missing_arguments_are_refused()
    {
        // Act
        Action noDefinition = () => Offering.Create(null!, Rules, new ImageLibrary());
        Action noRules = () => Offering.Create(Tour(), null!, new ImageLibrary());
        Action noImages = () => Offering.Create(Tour(), Rules, null!);
        Action noSchedule = () => Offering.Create(Tour() with { Schedule = null! }, Rules, new ImageLibrary());
        Action nullTranslation = () => Offering.Create(Tour() with { Translations = [null!] }, Rules, new ImageLibrary());

        // Assert
        noDefinition.ShouldThrow<ArgumentNullException>();
        noRules.ShouldThrow<ArgumentNullException>();
        noImages.ShouldThrow<ArgumentNullException>();
        noSchedule.ShouldThrow<ArgumentNullException>();
        nullTranslation.ShouldThrow<ArgumentNullException>();
    }
}
