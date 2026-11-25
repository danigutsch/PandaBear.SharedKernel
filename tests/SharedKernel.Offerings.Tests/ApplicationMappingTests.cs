namespace SharedKernel.Offerings.Tests;

/// <summary>
/// Tests that both ViajantesTurismo applications map onto the model despite their conflicts: nights against days, per-aggregate against
/// per-translation publication, a plain-text against a structured itinerary, four required prices against an optional list with labels,
/// and en-US/pt-BR against pt/en.
/// </summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "Offerings")]
public sealed class ApplicationMappingTests
{
    private static readonly ContentCulture AmericanEnglish = ContentCulture.Restore("en-US");
    private static readonly ContentCulture Brazilian = ContentCulture.Restore("pt-BR");

    [Fact]
    public void The_original_app_maps_a_single_language_scheduled_tour_with_its_four_required_prices()
    {
        // Arrange
        var cultures = SupportedCultures.Create(AmericanEnglish, [AmericanEnglish, Brazilian]).Value;
        var rules = new OfferingRules<OriginalPriceKind, NoMetric>(
            cultures,
            [new(OriginalPriceKind.Base, IsBase: true), new(OriginalPriceKind.SingleRoom), new(OriginalPriceKind.RegularBike), new(OriginalPriceKind.EBike)],
            []);
        var adminStart = new DateOnly(2027, 6, 7);
        var adminEnd = new DateOnly(2027, 6, 14);
        var adminDurationDays = adminEnd.DayNumber - adminStart.DayNumber;
        var days = DateOnlyRange.Create(adminStart, adminEnd).Value.Days;
        var definition = new OfferingDefinition<Guid, OriginalPriceKind, NoMetric>
        {
            Schedule = new OfferingScheduleDefinition(OfferingAvailability.Scheduled, adminStart, adminEnd, DurationDays: days),
            Currency = "EUR",
            Prices =
            [
                new(OriginalPriceKind.EBike, 300m),
                new(OriginalPriceKind.SingleRoom, 450m),
                new(OriginalPriceKind.RegularBike, 150m),
                new(OriginalPriceKind.Base, 1890.50m),
            ],
            Participants = new ParticipantRange(4, 12),
            Translations =
            [
                new OfferingTranslationDefinition
                {
                    Culture = "en-US",
                    Status = PublicationStatus.Published,
                    Title = "Alps by bike",
                    Summary = "Eight days in the Alps.",
                    Description = "Day 1: arrival in Innsbruck.\nDay 2: ride to Kufstein.",
                    Included = ["Hotels", "Luggage transfer"],
                },
            ],
        };

        // Act
        var offering = Offering.Create(definition, rules, new NoImages()).ShouldBeSuccess();
        var view = offering.PublishedIn(Brazilian, cultures).ShouldNotBeNull();
        var replayed = Offering.Restore(offering.ToDefinition(), rules);

        // Assert
        adminDurationDays.ShouldBe(7);
        offering.Schedule.DurationDays.ShouldBe(8);
        offering.Schedule.Nights.ShouldBe(adminDurationDays);
        offering.Prices.Select(line => line.Kind).ShouldBe([OriginalPriceKind.Base, OriginalPriceKind.SingleRoom, OriginalPriceKind.RegularBike, OriginalPriceKind.EBike]);
        offering.FromPrice.ShouldBe(Money.Restore(1890.50m, Currency.Eur));
        offering.Steps.ShouldBeEmpty();
        view.Culture.ShouldBe(AmericanEnglish);
        view.Description.ShouldBe(["Day 1: arrival in Innsbruck. Day 2: ride to Kufstein."]);
        view.CallToAction.ShouldBe(OfferingCallToAction.Contact);
        replayed.Revise(offering.ToDefinition(), rules, new NoImages()).ShouldBeSuccess().ShouldBeFalse();
    }

    [Fact]
    public void The_original_app_keeps_its_aggregate_gate_on_top_of_published_translations()
    {
        // Arrange
        var cultures = SupportedCultures.Create(AmericanEnglish, [AmericanEnglish, Brazilian]).Value;
        var rules = new OfferingRules<OriginalPriceKind, NoMetric>(cultures, [new(OriginalPriceKind.Base, IsBase: true)], []);
        var definition = new OfferingDefinition<Guid, OriginalPriceKind, NoMetric>
        {
            Schedule = new OfferingScheduleDefinition(OfferingAvailability.SoldOut, new DateOnly(2027, 6, 7), new DateOnly(2027, 6, 14), DurationDays: 8),
            Translations = [new OfferingTranslationDefinition { Culture = "en-US", Title = "Alps by bike", Status = PublicationStatus.Draft }],
        };

        // Act
        var draft = Offering.Create(definition, rules, new NoImages()).ShouldBeSuccess();

        // Assert
        draft.PublishedIn(AmericanEnglish, cultures).ShouldBeNull();
        draft.Schedule.Availability.ShouldBe(OfferingAvailability.SoldOut);
    }

    [Fact]
    public void V2_maps_its_five_price_kinds_with_labels_for_other_in_each_language()
    {
        // Arrange
        var definition = OfferingFixtures.Tour(
            OfferingFixtures.Translation(status: PublicationStatus.Published) with { PriceLabels = ["Passeio de barco", "Ingresso do castelo"] },
            OfferingFixtures.Translation("en", PublicationStatus.Published) with { PriceLabels = ["Boat trip", "Castle ticket"] }) with
        {
            Prices =
            [
                new(TourPriceKind.Other, 60m),
                new(TourPriceKind.BikeRental, 180m),
                new(TourPriceKind.EBikeRental, 320m),
                new(TourPriceKind.SingleRoomSupplement, 650m),
                new(TourPriceKind.PerPersonDoubleRoom, 2890m),
                new(TourPriceKind.Other, 25m),
            ],
        };

        // Act
        var offering = OfferingFixtures.Valid(definition);
        var portuguese = offering.PublishedIn(OfferingFixtures.Portuguese, OfferingFixtures.Cultures).ShouldNotBeNull();
        var english = offering.PublishedIn(ContentCulture.Restore("en-GB"), OfferingFixtures.Cultures).ShouldNotBeNull();

        // Assert
        portuguese.Prices.Select(price => (price.Kind, price.Label)).ShouldBe(
        [
            (TourPriceKind.PerPersonDoubleRoom, null),
            (TourPriceKind.SingleRoomSupplement, null),
            (TourPriceKind.BikeRental, null),
            (TourPriceKind.EBikeRental, null),
            (TourPriceKind.Other, "Passeio de barco"),
            (TourPriceKind.Other, "Ingresso do castelo"),
        ]);
        english.Culture.ShouldBe(OfferingFixtures.English);
        english.Prices.Where(price => price.Label is not null).Select(price => price.Label).ShouldBe(["Boat trip", "Castle ticket"]);
        portuguese.FromPrice.ShouldBe(Money.Restore(2890m, Currency.Eur));
    }
}
