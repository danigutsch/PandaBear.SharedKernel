using static PandaBear.Offerings.Tests.OfferingFixtures;

namespace PandaBear.Offerings.Tests;

/// <summary>Tests revising an offering, its round trip through a definition, and restoring a stored one.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Offerings")]
public sealed class OfferingLifecycleTests
{
    private readonly ImageLibrary _images = new();

    [Fact]
    public void A_created_offering_keeps_every_value()
    {
        // Arrange
        var cover = _images.Add(Portuguese);

        // Act
        var offering = Valid(FullTour(cover), _images);

        // Assert
        offering.SortOrder.ShouldBe(1);
        offering.SignUpLink.ShouldBe(WebUrl.Restore("https://forms.example.org/romantic-road"));
        offering.Participants.ShouldBe(new ParticipantRange(6, 16));
        offering.GroupCondition.ShouldNotBeNull().DueDays.ShouldBe(45);
        offering.PaymentTerms.ShouldBe(new PaymentTerms(30, 10, 60));
        offering.FromPrice.ShouldBe(Money.Restore(2890m, Currency.Eur));
        offering.CoverId.ShouldBe(cover);
        offering.Translations.Select(translation => translation.Culture).ShouldBe([Portuguese, English]);
    }

    [Fact]
    public void Revising_with_the_same_data_reports_no_change_and_other_data_a_change()
    {
        // Arrange
        var offering = Valid(Tour());

        // Act
        var same = offering.Revise(Tour(), Rules, _images).ShouldBeSuccess();
        var changed = offering.Revise(Tour() with { SortOrder = 7 }, Rules, _images).ShouldBeSuccess();

        // Assert
        same.ShouldBeFalse();
        changed.ShouldBeTrue();
        offering.SortOrder.ShouldBe(7);
    }

    [Fact]
    public void Reordering_the_translations_is_a_change()
    {
        // Arrange
        var offering = Valid(Tour(Translation(), Translation("en")));

        // Act
        var changed = offering.Revise(Tour(Translation("en"), Translation()), Rules, _images).ShouldBeSuccess();

        // Assert
        changed.ShouldBeTrue();
        offering.Translations[0].Culture.ShouldBe(English);
    }

    [Fact]
    public void A_refused_revision_leaves_the_offering_unchanged()
    {
        // Arrange
        var offering = Valid(Tour());

        // Act
        var result = offering.Revise(Tour() with { SortOrder = 9, Currency = "XYZ" }, Rules, _images);

        // Assert
        result.ErrorDetails.ShouldHaveValidationError(OfferingFields.Currency, "Currency is not a valid ISO 4217 currency code.");
        offering.SortOrder.ShouldBe(1);
    }

    [Fact]
    public void Revising_with_other_price_kind_flags_reports_no_change_but_follows_the_new_rules()
    {
        // Arrange
        var definition = Tour(Translation() with { StepTexts = null }) with { Steps = [] };
        var offering = Valid(definition);
        var rules = new TourRules(Cultures, [new(TourPriceKind.PerPersonDoubleRoom), new(TourPriceKind.SingleRoomSupplement, IsBase: true)], Metrics);

        // Act
        var changed = offering.Revise(definition, rules, _images).ShouldBeSuccess();

        // Assert
        changed.ShouldBeFalse();
        offering.FromPrice.ShouldBe(Money.Restore(650m, Currency.Eur));
    }

    [Fact]
    public void The_definition_of_an_offering_creates_the_same_offering()
    {
        // Arrange
        var cover = _images.Add(Portuguese);
        var offering = Valid(FullTour(cover), _images);

        // Act
        var definition = offering.ToDefinition();
        var again = Offering.Create(definition, Rules, _images).ShouldBeSuccess();
        var changed = offering.Revise(definition, Rules, _images).ShouldBeSuccess();

        // Assert
        changed.ShouldBeFalse();
        again.ToDefinition().Schedule.ShouldBe(definition.Schedule);
        definition.Schedule.Year.ShouldBeNull();
        definition.Currency.ShouldBe("EUR");
        definition.Prices.ShouldNotBeNull().Select(line => line.Kind).ShouldBe([TourPriceKind.PerPersonDoubleRoom, TourPriceKind.SingleRoomSupplement, TourPriceKind.Other]);
        definition.GroupCondition.ShouldBe(new GroupConditionDefinition(8, 150m, 45));
        var translation = definition.Translations.ShouldNotBeNull()[0];
        translation.Slug.ShouldBe("rota-romantica");
        translation.Description.ShouldBe("Primeiro parágrafo.\n\nSegundo parágrafo.");
        translation.PriceLabels.ShouldBe(["Barco"]);
        translation.StepTexts.ShouldNotBeNull()[1].ShouldBe(new ProgramStepTextDefinition("Rothenburg", "Ride.", "+ barco"));
    }

    [Fact]
    public void An_undated_offering_keeps_its_entered_year_in_the_definition()
    {
        // Arrange
        var offering = Valid(Tour() with { Schedule = Schedule(OfferingAvailability.DatesToBeConfirmed, year: 2028) });

        // Act
        var definition = offering.ToDefinition();

        // Assert
        definition.Schedule.ShouldBe(new OfferingScheduleDefinition(OfferingAvailability.DatesToBeConfirmed, null, null, 2028, 3));
    }

    [Fact]
    public void Restoring_a_definition_gives_the_same_offering_without_checking_lengths_or_publishing()
    {
        // Arrange
        var cover = _images.Add(Portuguese);
        var stored = Valid(FullTour(cover), _images).ToDefinition() with { SortOrder = 3 };
        var lax = stored with
        {
            Translations = [stored.Translations![0] with { Title = new string('x', 80), Summary = null }, stored.Translations[1]],
        };

        // Act
        var restored = Offering.Restore(stored, Rules);
        var notChecked = Offering.Restore(lax, Rules);

        // Assert
        restored.SortOrder.ShouldBe(3);
        restored.FromPrice.ShouldBe(Money.Restore(2890m, Currency.Eur));
        restored.Revise(stored, Rules, _images).ShouldBeSuccess().ShouldBeFalse();
        notChecked.Translations[0].Title.Length.ShouldBe(80);
        notChecked.IsPublishedIn(Portuguese).ShouldBeTrue();
    }

    [Fact]
    public void A_restored_offering_takes_its_from_price_and_labels_from_the_current_rules()
    {
        // Arrange
        var stored = Valid(FullTour(null), _images).ToDefinition();
        var rules = new TourRules(Cultures, [new(TourPriceKind.SingleRoomSupplement, IsBase: true), new(TourPriceKind.Other, Repeatable: true)], Metrics);

        // Act
        var restored = Offering.Restore(stored, rules);
        var view = restored.PublishedIn(Portuguese, Cultures).ShouldNotBeNull();

        // Assert
        restored.FromPrice.ShouldBe(Money.Restore(650m, Currency.Eur));
        view.Prices.Select(price => price.Label).ShouldBe([null, null, null]);
        view.Prices.Select(price => price.IsBase).ShouldBe([false, true, false]);
    }

    [Fact]
    public void Restoring_refuses_data_later_reads_cannot_handle()
    {
        // Arrange
        var stored = Valid(FullTour(null), _images).ToDefinition();
        var portuguese = stored.Translations![0];

        // Act & Assert
        ShouldNotRestore(stored with { Translations = [portuguese, portuguese] });
        ShouldNotRestore(stored with { Translations = [portuguese with { Culture = "not a culture!" }] });
        ShouldNotRestore(stored with { Translations = [portuguese with { Status = (PublicationStatus)7 }] });
        ShouldNotRestore(stored with { Translations = [portuguese with { Slug = "Not Canonical" }] });
        ShouldNotRestore(stored with { Translations = [portuguese with { Title = " " }] });
        ShouldNotRestore(stored with { Translations = [portuguese with { Description = "Broken \uD800" }] });
        ShouldNotRestore(stored with { Schedule = stored.Schedule with { Availability = 0 } });
        ShouldNotRestore(stored with { Schedule = stored.Schedule with { EndDate = null } });
        ShouldNotRestore(stored with { Schedule = stored.Schedule with { EndDate = new DateOnly(2027, 9, 1) } });
        ShouldNotRestore(stored with { Schedule = stored.Schedule with { DurationDays = 0 } });
        ShouldNotRestore(stored with { Currency = "EURO" });
        ShouldNotRestore(stored with { Currency = null });
        ShouldNotRestore(stored with { Prices = [new((TourPriceKind)42, 10m)] });
        ShouldNotRestore(stored with { Metrics = [new((TourMetric)42, 1m)] });
        ShouldNotRestore(stored with { Steps = [new TourStep(null, [new((TourMetric)42, 1m)])] });
        ShouldNotRestore(stored with { GroupCondition = new GroupConditionDefinition(8, null, 45) });
    }

    [Fact]
    public void Restoring_refuses_missing_arguments()
    {
        // Arrange
        var stored = Tour();

        // Act
        Action noDefinition = () => Offering.Restore<Guid, TourPriceKind, TourMetric>(null!, Rules);
        Action noRules = () => Offering.Restore(stored, null!);
        Action nullTranslation = () => Offering.Restore(stored with { Translations = [null!] }, Rules);

        // Assert
        noDefinition.ShouldThrow<ArgumentNullException>();
        noRules.ShouldThrow<ArgumentNullException>();
        nullTranslation.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Restoring_keeps_a_stored_culture_that_the_rules_no_longer_list()
    {
        // Arrange
        var stored = Tour(Translation() with { Slug = "rota" }, Translation("en") with { Culture = "es", Slug = "ruta" });

        // Act
        var restored = Offering.Restore(stored, Rules);

        // Assert
        restored.Translations[1].Culture.ShouldBe(ContentCulture.Restore("es"));
    }
}
