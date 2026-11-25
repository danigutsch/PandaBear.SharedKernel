using static SharedKernel.Offerings.Tests.OfferingFixtures;

namespace SharedKernel.Offerings.Tests;

/// <summary>Tests the language-neutral steps and their texts per culture.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "Offerings")]
public sealed class OfferingStepTests
{
    [Fact]
    public void Steps_keep_their_image_and_their_texts_per_culture()
    {
        // Arrange
        var images = new ImageLibrary();
        var image = images.Add(Portuguese);
        var definition = Tour() with { Steps = [new TourStep(image), new TourStep(null), new TourStep(null)] };

        // Act
        var offering = Valid(definition, images);

        // Assert
        offering.Steps.Count.ShouldBe(3);
        offering.Steps[0].ImageId.ShouldBe(image);
        var texts = offering.Translations[0].StepTexts;
        texts.Select(text => text.Title).ShouldBe(["Würzburg", "Rothenburg", "Füssen"]);
        texts[1].Paragraphs.ShouldBe(["Ride."]);
        texts[1].Note.ShouldBe("+ barco");
        texts[0].Note.ShouldBeNull();
    }

    [Fact]
    public void With_one_step_per_day_a_programme_is_empty_or_has_as_many_steps_as_days()
    {
        // Act
        var empty = Valid(Tour(Translation() with { StepTexts = null }) with { Steps = [] });
        var fewer = Refused(Tour(Translation() with { StepTexts = null }) with { Steps = [new TourStep(null), new TourStep(null)] });

        // Assert
        empty.Steps.ShouldBeEmpty();
        fewer.ShouldHaveValidationError(OfferingFields.Steps, "The number of steps must equal the duration in days.");
    }

    [Fact]
    public void With_a_free_step_count_any_number_up_to_the_maximum_is_allowed()
    {
        // Arrange
        var rules = new TourRules(Cultures, PriceKinds, Metrics) { MaxSteps = 5 };
        var steps = Enumerable.Repeat(new TourStep(null), 5).ToArray();

        // Act
        var five = Offering.Create(Tour(Translation() with { StepTexts = null }) with { Steps = steps }, rules, new ImageLibrary());
        var six = Offering.Create(Tour(Translation() with { StepTexts = null }) with { Steps = [.. steps, new TourStep(null)] }, rules, new ImageLibrary());

        // Assert
        five.ShouldBeSuccess().Steps.Count.ShouldBe(5);
        six.ErrorDetails.ShouldHaveValidationError(OfferingFields.Steps, "There cannot be more than 5 steps.");
    }

    [Fact]
    public void A_step_image_must_exist()
    {
        // Act
        var error = Refused(Tour() with { Steps = [new TourStep(null), new TourStep(Guid.CreateVersion7()), new TourStep(null)] });

        // Assert
        error.ShouldHaveValidationError(OfferingFields.StepImage(1), "The image does not exist.");
    }

    [Fact]
    public void A_translation_has_at_most_one_text_per_step()
    {
        // Act
        var error = Refused(Tour(Translation() with { StepTexts = [new("A"), new("B"), new("C"), new("D")] }));

        // Assert
        error.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.Steps), "There are more step texts than steps.");
    }

    [Theory]
    [InlineData("Dia 1: Chegada")]
    [InlineData("dia 2")]
    [InlineData("Dia1 Würzburg")]
    [InlineData("Day 3 - Füssen")]
    public void A_step_title_does_not_begin_with_a_day_number(string title)
    {
        // Act
        var error = Refused(Tour(Translation() with { StepTexts = [new(title), new("B"), new("C")] }));

        // Assert
        error.ShouldHaveValidationError("pt.steps[0].title", "Step 1 title must not start with a day number; the number comes from its position.");
    }

    [Theory]
    [InlineData("Diamantina")]
    [InlineData("Dia a dia em Lisboa")]
    [InlineData("Daytona")]
    public void A_step_title_may_begin_with_the_prefix_without_a_number(string title)
    {
        // Act
        var offering = Valid(Tour(Translation() with { StepTexts = [new(title), new("B"), new("C")] }));

        // Assert
        offering.Translations[0].StepTexts[0].Title.ShouldBe(title);
    }

    [Fact]
    public void A_step_text_needs_a_title_and_follows_the_text_limits()
    {
        // Act
        var error = Refused(Tour(Translation() with
        {
            StepTexts = [new("A"), new(" ", "Text"), new("C", new string('x', 4001), new string('n', 301))],
        }));

        // Assert
        error.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.StepTitle(1)), "Step 2 title is required.");
        error.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.StepText(2)), "Step 3 text: each paragraph must be at most 4000 characters.");
        error.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.StepNote(2)), "Step 3 note cannot exceed 300 characters.");
    }
}
