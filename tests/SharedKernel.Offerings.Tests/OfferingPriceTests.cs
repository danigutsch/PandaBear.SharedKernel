using static PandaBear.Offerings.Tests.OfferingFixtures;

namespace PandaBear.Offerings.Tests;

/// <summary>Tests the typed prices: kinds, order, amounts, the currency, the from price and the labels of labelled kinds.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Offerings")]
public sealed class OfferingPriceTests
{
    [Fact]
    public void Prices_are_sorted_into_the_kind_order_and_repeatable_kinds_keep_their_entered_order()
    {
        // Arrange
        var definition = Tour() with
        {
            Prices =
            [
                new(TourPriceKind.Other, 90m),
                new(TourPriceKind.EBikeRental, 320m),
                new(TourPriceKind.PerPersonDoubleRoom, 2890m),
                new(TourPriceKind.Other, 40m),
                new(TourPriceKind.SingleRoomSupplement, 650m),
            ],
        };

        // Act
        var offering = Valid(definition);

        // Assert
        offering.Prices.Select(line => (line.Kind, line.Amount.Amount)).ShouldBe(
        [
            (TourPriceKind.PerPersonDoubleRoom, 2890m),
            (TourPriceKind.SingleRoomSupplement, 650m),
            (TourPriceKind.EBikeRental, 320m),
            (TourPriceKind.Other, 90m),
            (TourPriceKind.Other, 40m),
        ]);
        offering.Prices.Select(line => line.Amount.Currency).Distinct().ShouldBe([Currency.Eur]);
        offering.Currency.ShouldBe(Currency.Eur);
    }

    [Fact]
    public void The_from_price_is_the_lowest_price_of_a_base_kind()
    {
        // Arrange
        var rules = new TourRules(Cultures, [new(TourPriceKind.PerPersonDoubleRoom, IsBase: true), new(TourPriceKind.Other, Repeatable: true, IsBase: true)], Metrics);
        var definition = Tour() with { Prices = [new(TourPriceKind.PerPersonDoubleRoom, 2890m), new(TourPriceKind.Other, 2490m), new(TourPriceKind.Other, 3100m)], Metrics = [] };

        // Act
        var offering = Offering.Create(definition, rules, new ImageLibrary()).ShouldBeSuccess();
        var withoutBase = Valid(Tour() with { Prices = [new(TourPriceKind.SingleRoomSupplement, 650m)] });

        // Assert
        offering.FromPrice.ShouldBe(Money.Restore(2490m, Currency.Eur));
        withoutBase.FromPrice.ShouldBeNull();
    }

    [Fact]
    public void A_kind_that_does_not_repeat_appears_once()
    {
        // Act
        var error = Refused(Tour() with { Prices = [new(TourPriceKind.BikeRental, 100m), new(TourPriceKind.BikeRental, 120m)] });

        // Assert
        Fields(error).ShouldBe([OfferingFields.PriceKind(1)]);
        error.ShouldHaveValidationError("prices[1].kind", "This price kind can appear only once.");
    }

    [Fact]
    public void A_kind_outside_the_rules_is_refused()
    {
        // Arrange
        var rules = new TourRules(Cultures, [new(TourPriceKind.PerPersonDoubleRoom, IsBase: true)], Metrics);

        // Act
        var outside = Offering.Create(Tour() with { Prices = [new(TourPriceKind.BikeRental, 100m)] }, rules, new ImageLibrary());
        var undefined = Refused(Tour() with { Prices = [new((TourPriceKind)42, 100m)] });

        // Assert
        outside.ErrorDetails.ShouldHaveValidationError(OfferingFields.PriceKind(0), "The price kind is not supported.");
        undefined.ShouldHaveValidationError(OfferingFields.PriceKind(0), "The price kind is not supported.");
    }

    [Theory]
    [InlineData("0", "Price must be greater than zero.")]
    [InlineData("-10", "Price must be greater than zero.")]
    [InlineData("50000.01", "Price cannot exceed 50000.")]
    [InlineData("1.500", "Price must have at most 2 decimal places, like 1500 or 1500.50.")]
    public void An_amount_must_be_positive_money_up_to_the_maximum(string amount, string message)
    {
        // Act
        var error = Refused(Tour() with { Prices = [new(TourPriceKind.PerPersonDoubleRoom, decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture))] });

        // Assert
        error.ShouldHaveValidationError("prices[0].amount", message);
    }

    [Fact]
    public void More_prices_than_the_maximum_are_refused()
    {
        // Act
        var error = Refused(Tour() with { Prices = [.. Enumerable.Repeat(new PriceLineDefinition<TourPriceKind>(TourPriceKind.Other, 10m), 7)] });

        // Assert
        error.ShouldHaveValidationError(OfferingFields.Prices, "There cannot be more than 6 prices.");
    }

    [Fact]
    public void Prices_and_a_group_condition_need_a_currency_and_a_given_currency_must_be_valid()
    {
        // Act
        var prices = Refused(Tour() with { Currency = null });
        var group = Refused(Tour() with { Currency = " ", Prices = [], GroupCondition = new(8, 150m, 45) });
        var unknown = Refused(Tour() with { Currency = "XYZ" });
        var none = Valid(Tour() with { Currency = null, Prices = [] });

        // Assert
        prices.ShouldHaveValidationError(OfferingFields.Currency, "Currency is required with prices or a group condition.");
        group.ShouldHaveValidationError(OfferingFields.Currency, "Currency is required with prices or a group condition.");
        unknown.ShouldHaveValidationError(OfferingFields.Currency, "Currency is not a valid ISO 4217 currency code.");
        none.Currency.ShouldBeNull();
        none.Prices.ShouldBeEmpty();
        none.FromPrice.ShouldBeNull();
    }

    [Fact]
    public void A_currency_without_prices_is_kept()
    {
        // Act
        var offering = Valid(Tour() with { Currency = "brl", Prices = [] });

        // Assert
        offering.Currency.ShouldBe(Currency.Brl);
    }

    [Fact]
    public void Each_price_of_a_labelled_kind_has_a_label_per_translation_in_price_order()
    {
        // Arrange
        var definition = Tour(Translation() with { PriceLabels = ["Barco no Reno", "Museu"] }) with
        {
            Prices = [new(TourPriceKind.Other, 90m), new(TourPriceKind.PerPersonDoubleRoom, 2890m), new(TourPriceKind.Other, 40m)],
        };

        // Act
        var offering = Valid(definition);

        // Assert
        offering.Translations[0].PriceLabels.ShouldBe(["Barco no Reno", "Museu"]);
    }

    [Fact]
    public void A_draft_may_lack_labels_but_not_have_more_labels_than_labelled_prices()
    {
        // Arrange
        var prices = new PriceLineDefinition<TourPriceKind>[] { new(TourPriceKind.PerPersonDoubleRoom, 2890m), new(TourPriceKind.Other, 90m) };

        // Act
        var lacking = Valid(Tour(Translation() with { PriceLabels = [] }) with { Prices = prices });
        var extra = Refused(Tour(Translation() with { PriceLabels = ["Barco", "Museu"] }) with { Prices = prices });

        // Assert
        lacking.Translations[0].PriceLabels.ShouldBeEmpty();
        extra.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.PriceLabels), "There are more price labels than prices that need one.");
    }

    [Fact]
    public void Publishing_needs_every_label_in_that_language()
    {
        // Arrange
        var images = new ImageLibrary();
        var prices = new PriceLineDefinition<TourPriceKind>[] { new(TourPriceKind.PerPersonDoubleRoom, 2890m), new(TourPriceKind.Other, 90m) };

        // Act
        var error = Refused(Tour(Translation(status: PublicationStatus.Published)) with { Prices = prices }, images);
        var published = Valid(Tour(Translation(status: PublicationStatus.Published) with { PriceLabels = ["Barco"] }) with { Prices = prices }, images);

        // Assert
        error.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.PriceLabels), "Every price of a kind that needs a label needs one in this language before publishing.");
        published.IsPublishedIn(Portuguese).ShouldBeTrue();
    }

    [Fact]
    public void A_blank_or_long_label_is_refused()
    {
        // Arrange
        var prices = new PriceLineDefinition<TourPriceKind>[] { new(TourPriceKind.Other, 90m), new(TourPriceKind.Other, 40m) };

        // Act
        var blank = Refused(Tour(Translation() with { PriceLabels = ["Barco", " "] }) with { Prices = prices });
        var tooLong = Refused(Tour(Translation() with { PriceLabels = [new string('x', 41)] }) with { Prices = prices });

        // Assert
        blank.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.PriceLabels), "Price labels cannot have an empty item.");
        tooLong.ShouldHaveValidationError(OfferingFields.Of(Portuguese, OfferingFields.PriceLabels), "Price labels: each item must be at most 40 characters.");
    }
}
