using System.Text.Json.Nodes;
using static SharedKernel.Offerings.Tests.OfferingFixtures;

namespace SharedKernel.Offerings.Tests;

/// <summary>Tests the schema.org JSON-LD of an offering's public view.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "Offerings")]
public sealed class OfferingStructuredDataTests
{
    private static readonly Uri Page = new("https://www.example.org/pt/viagens/rota-romantica");

    [Fact]
    public void A_tour_is_a_tourist_trip_with_its_offer_provider_and_itinerary()
    {
        // Arrange
        var view = PublishedView(Tour(Translation(status: PublicationStatus.Published)));
        var options = new OfferingStructuredDataOptions<TourPriceKind>
        {
            CanonicalUrl = Page,
            ImageUrls = [new Uri("https://www.example.org/media/cover.jpg")],
            Provider = new StructuredDataOrganization("Viajantes Turismo", new Uri("https://www.example.org/")),
            PriceLabel = (kind, culture) => culture == Portuguese
                ? kind == TourPriceKind.PerPersonDoubleRoom ? "Por pessoa em quarto duplo" : "Suplemento quarto individual"
                : "wrong culture",
        };

        // Act
        var json = OfferingStructuredData.Build(view, options);

        // Assert
        JsonNode.DeepEquals(json, JsonNode.Parse("""
            {
              "@context": "https://schema.org",
              "@type": "TouristTrip",
              "name": "Rota Romântica",
              "description": "Castelos e vinhedos de bicicleta.",
              "url": "https://www.example.org/pt/viagens/rota-romantica",
              "image": ["https://www.example.org/media/cover.jpg"],
              "provider": { "@type": "Organization", "name": "Viajantes Turismo", "url": "https://www.example.org/" },
              "offers": {
                "@type": "Offer",
                "price": 2890,
                "priceCurrency": "EUR",
                "availability": "https://schema.org/InStock",
                "url": "https://www.example.org/pt/viagens/rota-romantica",
                "priceSpecification": [
                  { "@type": "UnitPriceSpecification", "price": 2890, "priceCurrency": "EUR", "name": "Por pessoa em quarto duplo" },
                  { "@type": "UnitPriceSpecification", "price": 650, "priceCurrency": "EUR", "name": "Suplemento quarto individual" }
                ]
              },
              "itinerary": {
                "@type": "ItemList",
                "itemListElement": [
                  { "@type": "ListItem", "position": 1, "name": "Würzburg" },
                  { "@type": "ListItem", "position": 2, "name": "Rothenburg" },
                  { "@type": "ListItem", "position": 3, "name": "Füssen" }
                ]
              }
            }
            """)).ShouldBeTrue(json.ToJsonString());
    }

    [Theory]
    [InlineData(OfferingAvailability.Scheduled, "https://schema.org/InStock")]
    [InlineData(OfferingAvailability.SoldOut, "https://schema.org/SoldOut")]
    [InlineData(OfferingAvailability.DatesToBeConfirmed, "https://schema.org/PreOrder")]
    [InlineData(OfferingAvailability.OnRequest, "https://schema.org/MadeToOrder")]
    public void The_availability_maps_to_schema_org(OfferingAvailability availability, string expected)
    {
        // Arrange
        var schedule = availability is OfferingAvailability.OnRequest or OfferingAvailability.DatesToBeConfirmed
            ? Schedule(availability, year: availability == OfferingAvailability.OnRequest ? null : 2028)
            : Schedule(availability, new DateOnly(2027, 9, 20), new DateOnly(2027, 9, 22));
        var view = PublishedView(Tour(Translation(status: PublicationStatus.Published)) with { Schedule = schedule });

        // Act
        var json = OfferingStructuredData.Build(view, new OfferingStructuredDataOptions<TourPriceKind>());

        // Assert
        json["offers"]!["availability"]!.GetValue<string>().ShouldBe(expected);
    }

    [Fact]
    public void The_application_may_override_the_availability()
    {
        // Arrange
        var view = PublishedView(Tour(Translation(status: PublicationStatus.Published)));

        // Act
        var json = OfferingStructuredData.Build(view, new OfferingStructuredDataOptions<TourPriceKind> { Availability = OfferingAvailability.SoldOut });

        // Assert
        json["offers"]!["availability"]!.GetValue<string>().ShouldBe("https://schema.org/SoldOut");
    }

    [Fact]
    public void Several_base_prices_make_an_aggregate_offer_and_a_line_label_wins_over_the_kind_label()
    {
        // Arrange
        var rules = new TourRules(Cultures, [new(TourPriceKind.PerPersonDoubleRoom, IsBase: true), new(TourPriceKind.Other, Repeatable: true, NeedsLabel: true, IsBase: true)], Metrics);
        var definition = Tour(Translation(status: PublicationStatus.Published) with { PriceLabels = ["Quarto triplo"] }) with
        {
            Prices = [new(TourPriceKind.PerPersonDoubleRoom, 2890m), new(TourPriceKind.Other, 2490m)],
            Steps = [],
        };
        var view = Offering.Create(definition with { Translations = [definition.Translations![0] with { StepTexts = null }] }, rules, new ImageLibrary())
            .ShouldBeSuccess().PublishedIn(Portuguese, Cultures).ShouldNotBeNull();

        // Act
        var json = OfferingStructuredData.Build(view, new OfferingStructuredDataOptions<TourPriceKind> { PriceLabel = (_, _) => "Kind label" });

        // Assert
        var offers = json["offers"]!;
        offers["@type"]!.GetValue<string>().ShouldBe("AggregateOffer");
        offers["lowPrice"]!.GetValue<decimal>().ShouldBe(2490m);
        offers["highPrice"]!.GetValue<decimal>().ShouldBe(2890m);
        offers["offerCount"]!.GetValue<int>().ShouldBe(2);
        offers["price"].ShouldBeNull();
        offers["priceSpecification"]!.AsArray().Select(spec => spec!["name"]!.GetValue<string>()).ShouldBe(["Kind label", "Quarto triplo"]);
        json["itinerary"].ShouldBeNull();
    }

    [Fact]
    public void Price_names_are_asked_in_the_culture_of_the_text_shown()
    {
        // Arrange
        var view = Valid(Tour(Translation(status: PublicationStatus.Published))).PublishedIn(English, Cultures).ShouldNotBeNull();
        var asked = new List<ContentCulture>();

        // Act
        OfferingStructuredData.Build(view, new OfferingStructuredDataOptions<TourPriceKind>
        {
            PriceLabel = (_, culture) =>
            {
                asked.Add(culture);
                return null;
            },
        });

        // Assert
        view.Culture.ShouldBe(Portuguese);
        asked.Distinct().ShouldBe([Portuguese]);
    }

    [Fact]
    public void An_unspecified_or_undefined_availability_override_is_refused()
    {
        // Act
        Action unspecified = () => _ = new OfferingStructuredDataOptions<TourPriceKind> { Availability = OfferingAvailability.Unspecified };
        Action undefined = () => _ = new OfferingStructuredDataOptions<TourPriceKind> { Availability = (OfferingAvailability)99 };

        // Assert
        unspecified.ShouldThrow<ArgumentOutOfRangeException>();
        undefined.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_product_has_the_offer_but_no_provider_or_itinerary()
    {
        // Arrange
        var view = PublishedView(Tour(Translation(status: PublicationStatus.Published)));
        var options = new OfferingStructuredDataOptions<TourPriceKind>
        {
            Type = OfferingStructuredDataType.Product,
            Provider = new StructuredDataOrganization("Viajantes Turismo", null),
        };

        // Act
        var json = OfferingStructuredData.Build(view, options);

        // Assert
        json["@type"]!.GetValue<string>().ShouldBe("Product");
        json["offers"].ShouldNotBeNull();
        json["provider"].ShouldBeNull();
        json["itinerary"].ShouldBeNull();
    }

    [Fact]
    public void Only_what_the_page_shows_is_marked_up()
    {
        // Arrange
        var view = PublishedView(Tour(Translation(status: PublicationStatus.Published) with { Summary = "Resumo" }) with { Currency = null, Prices = [] });

        // Act
        var json = OfferingStructuredData.Build(view, new OfferingStructuredDataOptions<TourPriceKind>());

        // Assert
        json["offers"].ShouldBeNull();
        json["url"].ShouldBeNull();
        json["image"].ShouldBeNull();
        json["provider"].ShouldBeNull();
        json["description"]!.GetValue<string>().ShouldBe("Resumo");
        json.ToJsonString().ShouldNotContain("departureTime", StringComparison.Ordinal);
        json.ToJsonString().ShouldNotContain("arrivalTime", StringComparison.Ordinal);
    }

    [Fact]
    public void Prices_without_a_base_kind_have_no_offer()
    {
        // Arrange
        var view = PublishedView(Tour(Translation(status: PublicationStatus.Published)) with { Prices = [new(TourPriceKind.BikeRental, 200m)] });

        // Act
        var json = OfferingStructuredData.Build(view, new OfferingStructuredDataOptions<TourPriceKind>());

        // Assert
        json["offers"].ShouldBeNull();
    }

    [Fact]
    public void Urls_must_be_absolute_and_arguments_present()
    {
        // Arrange
        var view = PublishedView(Tour(Translation(status: PublicationStatus.Published)));

        // Act
        Action relativePage = () => _ = new OfferingStructuredDataOptions<TourPriceKind> { CanonicalUrl = new Uri("/pt/viagens", UriKind.Relative) };
        Action relativeImage = () => _ = new OfferingStructuredDataOptions<TourPriceKind> { ImageUrls = [new Uri("cover.jpg", UriKind.Relative)] };
        Action relativeProvider = () => _ = new StructuredDataOrganization("A", new Uri("/", UriKind.Relative));
        Action blankProvider = () => _ = new StructuredDataOrganization(" ", null);
        Action noView = () => OfferingStructuredData.Build<Guid, TourPriceKind, TourMetric>(null!, new OfferingStructuredDataOptions<TourPriceKind>());
        Action noOptions = () => OfferingStructuredData.Build(view, null!);

        // Assert
        relativePage.ShouldThrow<ArgumentException>();
        relativeImage.ShouldThrow<ArgumentException>();
        relativeProvider.ShouldThrow<ArgumentException>();
        blankProvider.ShouldThrow<ArgumentException>();
        noView.ShouldThrow<ArgumentNullException>();
        noOptions.ShouldThrow<ArgumentNullException>();
    }
}
