using static PandaBear.Offerings.Tests.OfferingFixtures;

namespace PandaBear.Offerings.Tests;

/// <summary>Tests the public view of an offering in a culture.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Offerings")]
public sealed class OfferingViewTests
{
    private static readonly ContentCulture Brazilian = ContentCulture.Restore("pt-BR");

    [Fact]
    public void The_view_carries_the_whole_published_translation_and_the_offering_data()
    {
        // Arrange
        var images = new ImageLibrary();
        var cover = images.Add(Portuguese);
        var step = images.Add(Portuguese);
        var offering = Valid(Tour(Translation(status: PublicationStatus.Published) with { PriceLabels = ["Barco"], DateNote = null, NotIncluded = ["Voos"], PracticalInfo = ["Chegada"] }) with
        {
            CoverId = cover,
            GalleryIds = [step],
            Prices = [new(TourPriceKind.Other, 90m), new(TourPriceKind.PerPersonDoubleRoom, 2890m)],
            GroupCondition = new(8, 150m, 45),
            PaymentTerms = new(30, 10, 60),
            Steps = [new TourStep(step, [new(TourMetric.Distance, 45m)]), new TourStep(null), new TourStep(null)],
        }, images);

        // Act
        var view = offering.PublishedIn(Portuguese, Cultures).ShouldNotBeNull();

        // Assert
        view.Culture.ShouldBe(Portuguese);
        view.Title.ShouldBe("Rota Romântica");
        view.Slug.Value.ShouldBe("rota-romantica");
        view.Eyebrow.ShouldBe("Alemanha");
        view.Subtitle.ShouldBeNull();
        view.Summary.ShouldBe("Castelos e vinhedos de bicicleta.");
        view.Description.ShouldBe(["Primeiro parágrafo.", "Segundo parágrafo."]);
        view.DateNote.ShouldBeNull();
        view.Highlights.Count.ShouldBe(3);
        view.Included.ShouldBe(["Hotel", "Bike"]);
        view.NotIncluded.ShouldBe(["Voos"]);
        view.PracticalInfo.ShouldBe(["Chegada"]);
        view.SortOrder.ShouldBe(1);
        view.Schedule.ShouldBe(offering.Schedule);
        view.Availability.ShouldBe(OfferingAvailability.Scheduled);
        view.Dates.ShouldBe(offering.Schedule.Dates);
        view.Year.ShouldBe(2027);
        view.DurationDays.ShouldBe(3);
        view.Nights.ShouldBe(2);
        view.Currency.ShouldBe(Currency.Eur);
        view.Prices.ShouldBe(
        [
            new PriceView<TourPriceKind>(TourPriceKind.PerPersonDoubleRoom, Money.Restore(2890m, Currency.Eur), null, true),
            new PriceView<TourPriceKind>(TourPriceKind.Other, Money.Restore(90m, Currency.Eur), "Barco", false),
        ]);
        view.FromPrice.ShouldBe(Money.Restore(2890m, Currency.Eur));
        view.Participants.ShouldBe(new ParticipantRange(6, 16));
        view.GroupCondition.ShouldBe(offering.GroupCondition);
        view.PaymentTerms.ShouldBe(new PaymentTerms(30, 10, 60));
        view.Metrics.ShouldBe(offering.Metrics);
        view.CoverId.ShouldBe(cover);
        view.GalleryIds.ShouldBe([step]);
        view.ImageIds.ShouldBe([cover, step]);
        var first = view.Steps[0];
        first.Number.ShouldBe(1);
        first.Title.ShouldBe("Würzburg");
        first.Paragraphs.ShouldBe(["Arrival."]);
        first.Note.ShouldBeNull();
        first.ImageId.ShouldBe(step);
        first.Metrics.ShouldBe([new Metric<TourMetric>(TourMetric.Distance, 45m)]);
        view.Steps[1].Number.ShouldBe(2);
        view.Steps[1].Note.ShouldBe("+ barco");
        view.Steps[1].ImageId.ShouldBeNull();
    }

    [Fact]
    public void The_seo_title_and_description_fall_back_to_the_title_and_summary()
    {
        // Arrange
        var offering = Valid(Tour(Translation(status: PublicationStatus.Published), Translation("en", PublicationStatus.Published) with { SeoTitle = "Bike the Romantic Road", SeoDescription = "Eight days." }));

        // Act
        var portuguese = offering.PublishedIn(Portuguese, Cultures).ShouldNotBeNull();
        var english = offering.PublishedIn(English, Cultures).ShouldNotBeNull();

        // Assert
        portuguese.SeoTitle.ShouldBe("Rota Romântica");
        portuguese.SeoDescription.ShouldBe("Castelos e vinhedos de bicicleta.");
        english.SeoTitle.ShouldBe("Bike the Romantic Road");
        english.SeoDescription.ShouldBe("Eight days.");
    }

    [Fact]
    public void A_view_falls_back_to_the_same_language_then_the_default_and_never_mixes_translations()
    {
        // Arrange
        var offering = Valid(Tour(Translation(status: PublicationStatus.Published), Translation("en") with { Title = "Draft only" }));

        // Act
        var brazilian = offering.PublishedIn(Brazilian, Cultures).ShouldNotBeNull();
        var english = offering.PublishedIn(English, Cultures).ShouldNotBeNull();
        var none = offering.PublishedIn(null, Cultures).ShouldNotBeNull();

        // Assert
        brazilian.Culture.ShouldBe(Portuguese);
        english.Culture.ShouldBe(Portuguese);
        english.Title.ShouldBe("Rota Romântica");
        english.Steps[0].Title.ShouldBe("Würzburg");
        none.Culture.ShouldBe(Portuguese);
    }

    [Fact]
    public void An_offering_without_a_published_translation_has_no_view()
    {
        // Arrange
        var offering = Valid(Tour());

        // Act
        var view = offering.PublishedIn(Portuguese, Cultures);
        Action noCultures = () => offering.PublishedIn(Portuguese, null!);

        // Assert
        view.ShouldBeNull();
        noCultures.ShouldThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData(OfferingAvailability.Scheduled, true, OfferingCallToAction.SignUp, true)]
    [InlineData(OfferingAvailability.Scheduled, false, OfferingCallToAction.Contact, false)]
    [InlineData(OfferingAvailability.SoldOut, true, OfferingCallToAction.Contact, false)]
    [InlineData(OfferingAvailability.DatesToBeConfirmed, true, OfferingCallToAction.Contact, false)]
    public void Only_a_scheduled_offering_with_a_link_asks_to_sign_up_and_shows_the_link(OfferingAvailability availability, bool link, OfferingCallToAction expected, bool shown)
    {
        // Arrange
        var offering = Valid(Tour(Translation(status: PublicationStatus.Published)) with
        {
            Schedule = Schedule(availability, new DateOnly(2027, 9, 20), new DateOnly(2027, 9, 22)),
            SignUpLink = link ? "https://forms.example.org/romantic-road" : null,
        });

        // Act
        var view = offering.PublishedIn(Portuguese, Cultures).ShouldNotBeNull();

        // Assert
        view.CallToAction.ShouldBe(expected);
        var linkShown = view.SignUpLink is not null;
        linkShown.ShouldBe(shown);
        offering.SignUpLink.ShouldBe(link ? WebUrl.Restore("https://forms.example.org/romantic-road") : null);
    }

    [Fact]
    public void An_offering_on_request_asks_for_contact_and_shows_its_note()
    {
        // Arrange
        var offering = Valid(Tour(Translation(status: PublicationStatus.Published) with { DateNote = "Sob medida" }) with { Schedule = Schedule(OfferingAvailability.OnRequest) });

        // Act
        var view = offering.PublishedIn(Portuguese, Cultures).ShouldNotBeNull();

        // Assert
        view.CallToAction.ShouldBe(OfferingCallToAction.Contact);
        view.DateNote.ShouldBe("Sob medida");
        view.Dates.ShouldBeNull();
        view.Year.ShouldBeNull();
    }
}
